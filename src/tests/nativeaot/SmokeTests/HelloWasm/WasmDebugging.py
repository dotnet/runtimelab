# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

import os
import sys
import lldb

# This will be called by LLDB when the .py file is loaded.
def __lldb_init_module(debugger, internal_dict):
    # Workaround: default progress reporting mangles stdout output badly.
    debugger.HandleCommand('settings set show-progress false')
    # Workaround: https://github.com/llvm/llvm-project/issues/61899.
    debugger.HandleCommand('settings set target.disable-aslr false')
    debugger.HandleCommand('b Main')
    debugger.HandleCommand('r')
    run_wasm_debugging_tests(debugger)

def run_wasm_debugging_tests(debugger):
    print('==== Commencing WASM debugging testing ====')
    target = debugger.GetSelectedTarget()
    process = target.GetProcess()
    thread = process.GetSelectedThread();
    if not setup_wasm_debugging_tests(debugger, target, thread):
        print('==== WASM debugging tests setup failed ====')
        exit_with_code(2)

    test_basic_types_display(target, process, thread)
    test_enum_display(target, process, thread)
    test_by_ref_display(target, process, thread)
    test_pointer_display(target, process, thread)
    test_string_display(target, process, thread)
    test_basic_array_display(target, process, thread)
    test_complex_array_display(target, process, thread)
    test_basic_multi_dimensional_array_display(target, process, thread)
    test_simple_struct_display(target, process, thread)
    test_simple_class_display(target, process, thread)
    test_derived_class_display(target, process, thread)
    test_recursive_class_display(target, process, thread)
    test_struct_instance_method(target, process, thread)
    test_class_instance_method(target, process, thread)
    test_variables(target, process, thread);

    if all_tests_passed:
        print('==== All WASM debugging tests passed ====')
        exit_with_code(100)
    else:
        print('==== Some WASM debugging tests failed ====')
        exit_with_code(1)

def exit_with_code(code):
    # Of all the different methods to exit LLDB from python with a specific code this is the only one that works.
    # It is quite hacky though, as LLDB doesn't get a chance to clean anything up...
    sys.stdout.flush()
    os._exit(code)

def setup_wasm_debugging_tests(debugger, target, thread):
    # First, set up the Debugger.Break breakpoint.
    dbp = target.BreakpointCreateByName('Break')
    if dbp.num_locations == 0:
        print(f'Failed to set up {dbp}')
        return False
    dbp.SetScriptCallbackBody('frame.thread.SetSelectedFrame(1)')

    # Second, __vmctx->set() to enable expression evaluation.
    # Trailing '; 0' is a workaround for https://discourse.llvm.org/t/lldb-expressions-unknown-error-is-returned-upon-successful-evaluation/78012/2.
    main_frame = thread.GetSelectedFrame()
    assert main_frame.GetFunctionName() == 'WasmDebugging_Program__Main', main_frame.GetFunctionName()
    vmctxt_set_result = main_frame.EvaluateExpression('__vmctx->set(); 0').GetError()
    if not vmctxt_set_result.success:
        print(f'__vmctx->set() failed: {vmctxt_set_result}')
        return False

    return True

def test_basic_types_display(target, process, thread):
    test_values_display_single(
        target, process, thread, 'test_basic_types_display',
        [('boolTrue', 'true'), ('boolFalse', 'false'),
         ('charA', 'U+0061'),
         ('i1', '\'\\x01\''), ('iM1', '\'\\xff\''), # These render as C++ 'char's, which is not ideal
         ('i2', '2'), ('iM2', '-2'),
         ('i3', '3'), ('iM3', '-3'),
         ('i4', '4'), ('iM4', '-4'),
         ('i5', '5'), ('iM5', '-5'),
         ('f1', '1'), ('d2', '2')],
        use_get_variable_path=True)

def test_enum_display(target, process, thread):
    test_values_display_single(
        target, process, thread, 'test_enum_display',
        [('dayOfWeek', 'Monday')],
        use_get_variable_path=True)

def test_by_ref_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_byref_display',
        [('(*p1).IntField', '1'), ('(*p1).FloatField', '2'),
         ('(*p2)->IntField', '1'), ('(*p2)->FloatField', '2')])

def test_pointer_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_pointer_display',
        [('p->IntField', '1'), ('p->FloatField', '2')])

def test_string_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_string_display',
        [('&s->_firstChar', 'u"Basic string"')], # TODO-LLVM-DI: improve string display.
        use_get_summary=True)

def test_basic_array_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_basic_array_display',
        [('p1->Data[0]', '1'), ('p1->Data[1]', '2'), ('p1->Data[2]', '3'), ('p1->Length', '3'), # TODO-LLVM-DI: improve array indexing.
         ('p2->Data[0]', '1'), ('p2->Data[1]', '2'), ('p2->Data[2]', '3'), ('p2->Length', '3')]) # TODO-LLVM-DI: improve array display.

def test_complex_array_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_complex_array_display',
        [('p1->Data[0]->IntField', '1'), ('p1->Data[0]->FloatField', '2'),
         ('p2->Data[0].IntField', '1'), ('p2->Data[0].FloatField', '2')])

def test_basic_multi_dimensional_array_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_basic_multi_dimensional_array_display',
        [('s->Length', '6'),
         ('sizeof(s->LowerBounds) / sizeof(*s->LowerBounds)', '2'), ('s->LowerBounds[0]', '0'), ('s->LowerBounds[1]', '0'),
         ('sizeof(s->Lengths) / sizeof(*s->Lengths)', '2'), ('s->Lengths[0]', '2'), ('s->Lengths[1]', '3'),
         ('s->Data[0]', '1'), ('s->Data[1]', '2'), ('s->Data[2]', '3'),
         ('s->Data[3]', '4'), ('s->Data[4]', '5'), ('s->Data[5]', '6')])

def test_simple_struct_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_simple_struct_display',
        [('s.IntField', '1'), ('s.FloatField', '2')],
        use_get_variable_path=True)

def test_simple_class_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_simple_class_display',
        [('s->IntField', '1'), ('s->FloatField', '2')])

def test_derived_class_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_derived_class_display',
        [('s->IntField', '1'), ('s->FloatField', '2'), ('s->LongField', '3')])

def test_recursive_class_display(target, process, thread):
    test_values_display_single(target, process, thread, 'test_recursive_class_display',
        [('s->Value', '1'), ('s->Next->Value', '2')])

def test_struct_instance_method(target, process, thread):
    test_values_display_single(target, process, thread, 'test_struct_instance_method',
        [('__this->IntField', '1'), ('__this->FloatField', '2')])

def test_class_instance_method(target, process, thread):
    test_values_display_single(target, process, thread, 'test_class_instance_method',
        [('__this->IntField', '1'), ('__this->FloatField', '2')])

def test_variables(target, process, thread):
    test_values_display(target, process, thread, "test_variables",
        [[('unusedBasicLocal', '1')],
         [('unusedClassLocal->IntField', '1'), ('unusedClassLocal->FloatField', '2')],
         [('index', '0')],
         [('index', '10')],
         [('classLocal->IntField', '1')],
         [('classLocal->IntField', '2')],
         [('structLocal.IntField', '1')],
         [('structLocal.IntField', '2')],
         [('p1', '5'), ('p2.IntField', '5'), ('p3->IntField', '5')],
         [('p2.IntField', '5'), ('p2.FloatField', '2'), ('structLocal.IntField', '5'), ('structLocal.FloatField', '2')],
         [('p3->IntField', '5'), ('p3->FloatField', '2'), ('classLocal->IntField', '5'), ('classLocal->FloatField', '2')]])

def test_values_display_single(target, process, thread, pyname, expected_values, use_get_variable_path=False, use_get_summary=False):
    test_values_display(target, process, thread, pyname, [expected_values], use_get_variable_path, use_get_summary)

def test_values_display(target, process, thread, pyname, expected_states, use_get_variable_path=False, use_get_summary=False):
    start_test(pyname)
    for expected_values in expected_states:
        process.Continue()
        frame = thread.GetSelectedFrame()
        print(f'Inspecting: {frame}')
        for (name, value) in expected_values:
            if use_get_variable_path:
                actual_sb_value = frame.GetValueForVariablePath(name)
            else:
                actual_sb_value = frame.EvaluateExpression(name)
            if use_get_summary:
                actual_value_as_string = actual_sb_value.GetSummary()
            else:
                actual_value_as_string = actual_sb_value.GetValue()
            if actual_value_as_string != value:
                fail_test(f'unexpected "{name}": "{actual_value_as_string}" (expected "{value}")')
                return
    pass_test()

all_tests_passed = True
current_test_name = None

def start_test(name):
    global current_test_name
    current_test_name = name
    print(f'== [{name}] started')

def end_test(success, details):
    global current_test_name
    assert current_test_name is not None
    if success:
        print(f'== [{current_test_name}] passed')
    else:
        print(f'== [{current_test_name}] failed ({details})')
        global all_tests_passed
        all_tests_passed = False
    current_test_name = None

def pass_test():
    end_test(True, None)

def fail_test(details):
    end_test(False, details)
