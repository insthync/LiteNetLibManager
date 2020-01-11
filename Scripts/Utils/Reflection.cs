using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace LiteNetLibManager.Utils
{
    public class Reflection
    {
        private static readonly Dictionary<string, ObjectActivator> objectActivators = new Dictionary<string, ObjectActivator>();
        private static string tempTypeName;

        // Improve reflection constructor performance with Linq expression (https://rogerjohansson.blog/2008/02/28/linq-expressions-creating-objects/)
        public delegate object ObjectActivator();
        public static ObjectActivator GetActivator(Type type)
        {
            tempTypeName = type.Name;
            if (!objectActivators.ContainsKey(tempTypeName))
            {
                if (type.IsClass)
                    objectActivators.Add(tempTypeName, Expression.Lambda<ObjectActivator>(Expression.New(type)).Compile());
                else
                    objectActivators.Add(tempTypeName, Expression.Lambda<ObjectActivator>(Expression.Convert(Expression.New(type), typeof(object))).Compile());
            }
            return objectActivators[tempTypeName];
        }
    }
}
