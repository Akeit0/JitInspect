using System.Reflection;
using System.Reflection.Emit;
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