using System;

namespace TechCosmos.RequireOneOf
{
        /// <summary>
        /// 让代码依赖的组件类型可以在编辑器里由使用者从一组互斥实现中任选一个，
        /// 而不是写死在代码里。同组只能存在一个：缺失自动补挂，重复删除旧的，
        /// Inspector 中可一键切换并迁移相同字段值。
        /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class RequireOneOfAttribute : Attribute
    {
        public Type[] Types { get; }

        /// <summary>true：只写了一个抽象类或接口，名单由编辑器收集其具体实现。</summary>
        public bool FromBase { get; }

        public RequireOneOfAttribute(Type type)
        {
            Types = new[] { type };
            FromBase = type != null && (type.IsAbstract || type.IsInterface);
        }

        public RequireOneOfAttribute(Type first, Type second, params Type[] more)
        {
            FromBase = false;
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
