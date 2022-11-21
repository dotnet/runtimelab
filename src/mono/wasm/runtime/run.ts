<<<<<<< HEAD
import { Module } from "./imports";
=======
import { Module, quit } from "./imports";
>>>>>>> 562aea2cb7d449d6e2e697df8cac56b599ec564d
import { mono_call_assembly_entry_point } from "./method-calls";
import { mono_wasm_set_main_args, runtime_is_initialized_reject } from "./startup";


export async function mono_run_main_and_exit(main_assembly_name: string, args: string[]): Promise<void> {
    try {
        const result = await mono_run_main(main_assembly_name, args);
        set_exit_code(result);
    } catch (error) {
        set_exit_code(1, error);
    }
}

export async function mono_run_main(main_assembly_name: string, args: string[]): Promise<number> {
    mono_wasm_set_main_args(main_assembly_name, args);
    return mono_call_assembly_entry_point(main_assembly_name, [args], "m");
}

// eslint-disable-next-line @typescript-eslint/explicit-module-boundary-types
export function mono_on_abort(error: any): void {
    runtime_is_initialized_reject(error);
    set_exit_code(1, error);
}

function set_exit_code(exit_code: number, reason?: any) {
    if (reason) {
        Module.printErr(reason.toString());
        if (reason.stack) {
            Module.printErr(reason.stack);
        }
    }
<<<<<<< HEAD
    const globalThisAny: any = globalThis;
    if (typeof globalThisAny.exit === "function") {
        globalThisAny.exit(exit_code);
    }
    else if (typeof globalThisAny.quit === "function") {
        globalThisAny.quit(exit_code);
    }
=======
    quit(exit_code, reason);
>>>>>>> 562aea2cb7d449d6e2e697df8cac56b599ec564d
}
