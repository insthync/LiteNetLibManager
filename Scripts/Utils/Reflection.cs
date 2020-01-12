using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection.Emit;

namespace LiteNetLibManager.Utils
{
    public class Reflection
    {
        // Improve reflection constructor performance with Linq expression (https://rogerjohansson.blog/2008/02/28/linq-expressions-creating-objects/)
        private static readonly Dictionary<string, Func<object>> expressionActivators = new Dictionary<string, Func<object>>();
        public static object CreateInstanceWithExpression(Type type)
        {
            if (!expressionActivators.ContainsKey(type.FullName))
            {
                if (type.IsClass)
                    expressionActivators.Add(type.FullName, Expression.Lambda<Func<object>>(Expression.New(type)).Compile());
                else
                    expressionActivators.Add(type.FullName, Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(type), typeof(object))).Compile());
            }
            return expressionActivators[type.FullName].Invoke();
        }

#if NET_4_6
        private static readonly Dictionary<string, DynamicMethod> dynamicMethodActivators = new Dictionary<string, DynamicMethod>();
        public static object CreateInstanceWithDynamicMethod(Type type)
        {
            if (!dynamicMethodActivators.ContainsKey(type.FullName))
            {
                DynamicMethod method = new DynamicMethod("", typeof(object), Type.EmptyTypes);
                ILGenerator il = method.GetILGenerator();

                if (type.IsValueType)
                {
                    var local = il.DeclareLocal(type);
                    il.Emit(OpCodes.Ldloc, local);
                    il.Emit(OpCodes.Box, type);
                    il.Emit(OpCodes.Ret);
                }
                else
                {
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    il.Emit(OpCodes.Newobj, ctor);
                    il.Emit(OpCodes.Ret);
                }
                dynamicMethodActivators.Add(type.FullName, method);
            }
            return dynamicMethodActivators[type.FullName].Invoke(null, null);
        }
#endif
    }
}
