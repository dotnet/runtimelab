// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
/* eslint-disable prefer-rest-params */

import NativeAOT from "consts:nativeAOT";
import BuildConfiguration from "consts:configuration";

import MonoWasmThreads from "consts:monoWasmThreads";
import { Module, loaderHelpers, mono_assert, runtimeHelpers } from "./globals";
import { bind_arg_marshal_to_cs } from "./marshal-to-cs";
import { marshal_exception_to_js, bind_arg_marshal_to_js } from "./marshal-to-js";
import {
    get_arg, get_sig, get_signature_argument_count, is_args_exception,
    bound_cs_function_symbol, get_signature_version, alloc_stack_frame, get_signature_type,
} from "./marshal";
import { mono_wasm_new_external_root, mono_wasm_new_root } from "./roots";
import { monoStringToString } from "./strings";
import { utf16ToString } from "./strings";
import { MonoObjectRef, MonoStringRef, MonoString, MonoObject, MonoMethod, JSMarshalerArguments, JSFunctionSignature, BoundMarshalerToCs, BoundMarshalerToJs, VoidPtrNull, MonoObjectRefNull, MonoObjectNull, MarshalerType } from "./types/internal";
import { Int32Ptr } from "./types/emscripten";
import cwraps from "./cwraps";
import { assembly_load } from "./class-loader";
import { assert_bindings, wrap_error_root, wrap_no_error_root } from "./invoke-js";
import { startMeasure, MeasuredBlock, endMeasure } from "./profiler";
import { mono_log_debug } from "./logging";
import { assert_synchronization_context } from "./pthreads/shared";

// function mono_wasm_bind_cs_function_naot(fully_qualified_name: CharPtr, fully_qualified_name_length: number, signature_hash: number, signature: JSFunctionSignature, is_exception: Int32Ptr): void
export function mono_wasm_bind_cs_function(fully_qualified_name: MonoStringRef, signature_hash: number, signature: JSFunctionSignature, is_exception: Int32Ptr, result_address: MonoObjectRef): void {
    assert_bindings();
    const fqn_root = mono_wasm_new_external_root<MonoString>(fully_qualified_name), resultRoot = mono_wasm_new_external_root<MonoObject>(result_address);
    const mark = startMeasure();
    try {
        if (NativeAOT) {
            signature_hash = arguments[2];
            signature = arguments[3];
            is_exception = arguments[4];
        }
        const version = get_signature_version(signature);
        mono_assert(version === 2, () => `Signature version ${version} mismatch.`);

        const args_count = get_signature_argument_count(signature);
        const js_fqn = NativeAOT ? utf16ToString(arguments[0], arguments[0] + 2 * arguments[1]) : monoStringToString(fqn_root)!;
        mono_assert(js_fqn, "fully_qualified_name must be string");

        mono_log_debug(`Binding [JSExport] ${js_fqn}`);

        const { assembly, namespace, classname, methodname } = parseFQN(js_fqn);

        let method = null;
        if (NativeAOT) {
            const wrapper_name = fixupSymbolName(`${js_fqn}_${signature_hash}`);
            method = (Module as any)["_" + wrapper_name];
            if (!method)
                throw new Error(`Could not find method: ${wrapper_name} in ${js_fqn}`);
        } else {
            const asm = assembly_load(assembly);
            if (!asm)
                throw new Error("Could not find assembly: " + assembly);

            const klass = cwraps.mono_wasm_assembly_find_class(asm, namespace, classname);
            if (!klass)
                throw new Error("Could not find class: " + namespace + ":" + classname + " in assembly " + assembly);

            const wrapper_name = `__Wrapper_${methodname}_${signature_hash}`;
            method = cwraps.mono_wasm_assembly_find_method(klass, wrapper_name, -1);
            if (!method)
                throw new Error(`Could not find method: ${wrapper_name} in ${klass} [${assembly}]`);
        }

        const arg_marshalers: (BoundMarshalerToCs)[] = new Array(args_count);
        for (let index = 0; index < args_count; index++) {
            const sig = get_sig(signature, index + 2);
            const marshaler_type = get_signature_type(sig);
            if (marshaler_type == MarshalerType.Task) {
                assert_synchronization_context();
            }
            const arg_marshaler = bind_arg_marshal_to_cs(sig, marshaler_type, index + 2);
            mono_assert(arg_marshaler, "ERR43: argument marshaler must be resolved");
            arg_marshalers[index] = arg_marshaler;
        }

        const res_sig = get_sig(signature, 1);
        const res_marshaler_type = get_signature_type(res_sig);
        if (res_marshaler_type == MarshalerType.Task) {
            assert_synchronization_context();
        }
        const res_converter = bind_arg_marshal_to_js(res_sig, res_marshaler_type, 1);

        const closure: BindingClosure = {
            method,
            fqn: js_fqn,
            args_count,
            arg_marshalers,
            res_converter,
            isDisposed: false,
        };
        let bound_fn: Function;
        if (args_count == 0 && !res_converter) {
            bound_fn = bind_fn_0V(closure);
        }
        else if (args_count == 1 && !res_converter) {
            bound_fn = bind_fn_1V(closure);
        }
        else if (args_count == 1 && res_converter) {
            bound_fn = bind_fn_1R(closure);
        }
        else if (args_count == 2 && res_converter) {
            bound_fn = bind_fn_2R(closure);
        }
        else {
            bound_fn = bind_fn(closure);
        }

        // this is just to make debugging easier. 
        // It's not CSP compliant and possibly not performant, that's why it's only enabled in debug builds
        // in Release configuration, it would be a trimmed by rollup
        if (BuildConfiguration === "Debug" && !runtimeHelpers.cspPolicy) {
            try {
                bound_fn = new Function("fn", "return (function JSExport_" + methodname + "(){ return fn.apply(this, arguments)});")(bound_fn);
            }
            catch (ex) {
                runtimeHelpers.cspPolicy = true;
            }
        }

        (<any>bound_fn)[bound_cs_function_symbol] = closure;

        _walk_exports_to_set_function(assembly, namespace, classname, methodname, signature_hash, bound_fn);
        endMeasure(mark, MeasuredBlock.bindCsFunction, js_fqn);
        wrap_no_error_root(is_exception, resultRoot);
    }
    catch (ex: any) {
        Module.err(ex.toString());
        wrap_error_root(is_exception, ex, resultRoot);
    } finally {
        resultRoot.release();
        fqn_root.release();
    }
}

const s_charsToReplace = [".", "-", "+"];

function fixupSymbolName(name: string) {
    // Sync with JSExportGenerator.FixupSymbolName
    let result = "";
    for (let index = 0; index < name.length; index++) {
        const b = name[index];
        if ((b >= "0" && b <= "9") ||
            (b >= "a" && b <= "z") ||
            (b >= "A" && b <= "Z") ||
            (b == "_")) {
            result += b;
        } else if( s_charsToReplace.includes(b)) {
            result += "_";
        } else {
            result += `_${b.charCodeAt(0).toString(16).toUpperCase()}_`;
        }
    }

    return result;
}

function bind_fn_0V(closure: BindingClosure) {
    const method = closure.method;
    const fqn = closure.fqn;
    if (!MonoWasmThreads) (<any>closure) = null;
    return function bound_fn_0V() {
        const mark = startMeasure();
        loaderHelpers.assert_runtime_running();
        mono_assert(!MonoWasmThreads || !closure.isDisposed, "The function was already disposed");
        const sp = Module.stackSave();
        try {
            const args = alloc_stack_frame(2);
            // call C# side
            invoke_method_and_handle_exception(method, args);
        } finally {
            Module.stackRestore(sp);
            endMeasure(mark, MeasuredBlock.callCsFunction, fqn);
        }
    };
}

function bind_fn_1V(closure: BindingClosure) {
    const method = closure.method;
    const marshaler1 = closure.arg_marshalers[0]!;
    const fqn = closure.fqn;
    if (!MonoWasmThreads) (<any>closure) = null;
    return function bound_fn_1V(arg1: any) {
        const mark = startMeasure();
        loaderHelpers.assert_runtime_running();
        mono_assert(!MonoWasmThreads || !closure.isDisposed, "The function was already disposed");
        const sp = Module.stackSave();
        try {
            const args = alloc_stack_frame(3);
            marshaler1(args, arg1);

            // call C# side
            invoke_method_and_handle_exception(method, args);
        } finally {
            Module.stackRestore(sp);
            endMeasure(mark, MeasuredBlock.callCsFunction, fqn);
        }
    };
}

function bind_fn_1R(closure: BindingClosure) {
    const method = closure.method;
    const marshaler1 = closure.arg_marshalers[0]!;
    const res_converter = closure.res_converter!;
    const fqn = closure.fqn;
    if (!MonoWasmThreads) (<any>closure) = null;
    return function bound_fn_1R(arg1: any) {
        const mark = startMeasure();
        loaderHelpers.assert_runtime_running();
        mono_assert(!MonoWasmThreads || !closure.isDisposed, "The function was already disposed");
        const sp = Module.stackSave();
        try {
            const args = alloc_stack_frame(3);
            marshaler1(args, arg1);

            // call C# side
            invoke_method_and_handle_exception(method, args);

            const js_result = res_converter(args);
            return js_result;
        } finally {
            Module.stackRestore(sp);
            endMeasure(mark, MeasuredBlock.callCsFunction, fqn);
        }
    };
}

function bind_fn_2R(closure: BindingClosure) {
    const method = closure.method;
    const marshaler1 = closure.arg_marshalers[0]!;
    const marshaler2 = closure.arg_marshalers[1]!;
    const res_converter = closure.res_converter!;
    const fqn = closure.fqn;
    if (!MonoWasmThreads) (<any>closure) = null;
    return function bound_fn_2R(arg1: any, arg2: any) {
        const mark = startMeasure();
        loaderHelpers.assert_runtime_running();
        mono_assert(!MonoWasmThreads || !closure.isDisposed, "The function was already disposed");
        const sp = Module.stackSave();
        try {
            const args = alloc_stack_frame(4);
            marshaler1(args, arg1);
            marshaler2(args, arg2);

            // call C# side
            invoke_method_and_handle_exception(method, args);

            const js_result = res_converter(args);
            return js_result;
        } finally {
            Module.stackRestore(sp);
            endMeasure(mark, MeasuredBlock.callCsFunction, fqn);
        }
    };
}

function bind_fn(closure: BindingClosure) {
    const args_count = closure.args_count;
    const arg_marshalers = closure.arg_marshalers;
    const res_converter = closure.res_converter;
    const method = closure.method;
    const fqn = closure.fqn;
    if (!MonoWasmThreads) (<any>closure) = null;
    return function bound_fn(...js_args: any[]) {
        const mark = startMeasure();
        loaderHelpers.assert_runtime_running();
        mono_assert(!MonoWasmThreads || !closure.isDisposed, "The function was already disposed");
        const sp = Module.stackSave();
        try {
            const args = alloc_stack_frame(2 + args_count);
            for (let index = 0; index < args_count; index++) {
                const marshaler = arg_marshalers[index];
                if (marshaler) {
                    const js_arg = js_args[index];
                    marshaler(args, js_arg);
                }
            }

            // call C# side
            invoke_method_and_handle_exception(method, args);

            if (res_converter) {
                const js_result = res_converter(args);
                return js_result;
            }
        } finally {
            Module.stackRestore(sp);
            endMeasure(mark, MeasuredBlock.callCsFunction, fqn);
        }
    };
}

type BindingClosure = {
    fqn: string,
    args_count: number,
    method: MonoMethod,
    arg_marshalers: (BoundMarshalerToCs)[],
    res_converter: BoundMarshalerToJs | undefined,
    isDisposed: boolean,
}

function invoke_method_and_handle_exception_mono(method: MonoMethod, args: JSMarshalerArguments): void {
    assert_bindings();
    const fail_root = mono_wasm_new_root<MonoString>();
    try {
        const fail = cwraps.mono_wasm_invoke_method_bound(method, args, fail_root.address);
        if (fail) throw new Error("ERR24: Unexpected error: " + monoStringToString(fail_root));
        if (is_args_exception(args)) {
            const exc = get_arg(args, 0);
            throw marshal_exception_to_js(exc);
        }
    }
    finally {
        fail_root.release();
    }
}

function invoke_method_and_handle_exception_naot(method: Function, args: JSMarshalerArguments): void {
    method(args);
    if (is_args_exception(args)) {
        const exc = get_arg(args, 0);
        throw marshal_exception_to_js(exc);
    }
}

export const invoke_method_and_handle_exception: (method: any, args: JSMarshalerArguments) => void = NativeAOT ? invoke_method_and_handle_exception_naot : invoke_method_and_handle_exception_mono;

export const exportsByAssembly: Map<string, any> = new Map();
function _walk_exports_to_set_function(assembly: string, namespace: string, classname: string, methodname: string, signature_hash: number, fn: Function): void {
    const parts = `${namespace}.${classname}`.replace(/\//g, ".").split(".");
    let scope: any = undefined;
    let assemblyScope = exportsByAssembly.get(assembly);
    if (!assemblyScope) {
        assemblyScope = {};
        exportsByAssembly.set(assembly, assemblyScope);
        exportsByAssembly.set(assembly + ".dll", assemblyScope);
    }
    scope = assemblyScope;
    for (let i = 0; i < parts.length; i++) {
        const part = parts[i];
        if (part != "") {
            let newscope = scope[part];
            if (typeof newscope === "undefined") {
                newscope = {};
                scope[part] = newscope;
            }
            mono_assert(newscope, () => `${part} not found while looking up ${classname}`);
            scope = newscope;
        }
    }

    if (!scope[methodname]) {
        scope[methodname] = fn;
    }
    scope[`${methodname}.${signature_hash}`] = fn;
}

async function mono_wasm_get_assembly_exports_mono(assembly: string): Promise<any> {
    assert_bindings();
    const result = exportsByAssembly.get(assembly);
    if (!result) {
        const mark = startMeasure();
        const asm = assembly_load(assembly);
        if (!asm)
            throw new Error("Could not find assembly: " + assembly);

        const klass = cwraps.mono_wasm_assembly_find_class(asm, runtimeHelpers.runtime_interop_namespace, "__GeneratedInitializer");
        if (klass) {
            const method = cwraps.mono_wasm_assembly_find_method(klass, "__Register_", -1);
            if (method) {
                const outException = mono_wasm_new_root();
                const outResult = mono_wasm_new_root<MonoString>();
                try {
                    cwraps.mono_wasm_invoke_method_ref(method, MonoObjectRefNull, VoidPtrNull, outException.address, outResult.address);
                    if (outException.value !== MonoObjectNull) {
                        const msg = monoStringToString(outResult)!;
                        throw new Error(msg);
                    }
                }
                finally {
                    outException.release();
                    outResult.release();
                }
            }
        } else {
            mono_assert(!MonoWasmThreads, () => `JSExport with multi-threading enabled is not supported with assembly ${assembly} as it was generated with the .NET 7 SDK`);
            // this needs to stay here for compatibility with assemblies generated in Net7
            // it doesn't have the __GeneratedInitializer class
            cwraps.mono_wasm_runtime_run_module_cctor(asm);
        }
        endMeasure(mark, MeasuredBlock.getAssemblyExports, assembly);
    }

    return exportsByAssembly.get(assembly) || {};
}

async function mono_wasm_get_assembly_exports_naot(assembly: string): Promise<any> {
    assert_bindings();
    const result = exportsByAssembly.get(assembly);
    if (!result) {
        const mark = startMeasure();
        
        let assemblyWithoutExtension = assembly;
        if (assemblyWithoutExtension.endsWith(".dll")) {
            assemblyWithoutExtension = assemblyWithoutExtension.substring(0, assembly.length - 4);
        }
        const register = (Module as any)["_" + assemblyWithoutExtension + "__GeneratedInitializer" + "__Register_"];
        mono_assert(register, `Missing wasm export for JSExport registration function in assembly ${assembly}`);
        register();

        endMeasure(mark, MeasuredBlock.getAssemblyExports, assembly);
    }

    return exportsByAssembly.get(assembly) || {};
}

export const mono_wasm_get_assembly_exports = NativeAOT ? mono_wasm_get_assembly_exports_naot : mono_wasm_get_assembly_exports_mono;

export function parseFQN(fqn: string)
    : { assembly: string, namespace: string, classname: string, methodname: string } {
    const assembly = fqn.substring(fqn.indexOf("[") + 1, fqn.indexOf("]")).trim();
    fqn = fqn.substring(fqn.indexOf("]") + 1).trim();

    const methodname = fqn.substring(fqn.indexOf(":") + 1);
    fqn = fqn.substring(0, fqn.indexOf(":")).trim();

    let namespace = "";
    let classname = fqn;
    if (fqn.indexOf(".") != -1) {
        const idx = fqn.lastIndexOf(".");
        namespace = fqn.substring(0, idx);
        classname = fqn.substring(idx + 1);
    }

    if (!assembly.trim())
        throw new Error("No assembly name specified " + fqn);
    if (!classname.trim())
        throw new Error("No class name specified " + fqn);
    if (!methodname.trim())
        throw new Error("No method name specified " + fqn);
    return { assembly, namespace, classname, methodname };
}
