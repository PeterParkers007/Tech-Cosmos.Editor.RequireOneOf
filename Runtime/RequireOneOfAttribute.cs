using System;

namespace TechCosmos.RequireOneOf
{
    /// <summary>
    /// 一组组件里有且仅有一个。没有就补名单第一个；再挂同组另一个会拆掉旧的。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class RequireOneOfAttribute : Attribute
    {
        public Type[] Types { get; }

        public RequireOneOfAttribute(Type first, Type second, params Type[] more)
        {
            if (more == null || more.Length == 0)
            {
                Types = new[] { first, second };
                return;
            }

            Types = new Type[2 + more.Length];
            Types[0] = first;
            Types[1] = second;
            Array.Copy(more, 0, Types, 2, more.Length);
        }
    }
}
