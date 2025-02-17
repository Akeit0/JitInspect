using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.AbstractDac;
using Microsoft.Diagnostics.Runtime.Utilities;
using FieldInfo = System.Reflection.FieldInfo;
using MethodInfo = System.Reflection.MethodInfo;

namespace JitInspect;

internal static class FunctionPointerHelper
{
    public static IntPtr GetMethodPointer(MethodInfo methodInfo)
    {
        var dynamicMethod = new DynamicMethod("GetFunctionPointer", typeof(IntPtr), [], typeof(FunctionPointerHelper).Module);
        var il = dynamicMethod.GetILGenerator();
        il.Emit(OpCodes.Ldftn, methodInfo);
        il.Emit(OpCodes.Ret);
        return (IntPtr)dynamicMethod.Invoke(null, null)!;
    }
}