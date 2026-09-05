using System;

namespace TechCosmos.RequireOneOf
{
        /// <summary>
        /// 一组组件里有且仅有一个。点名具体类时补名单第一个；写抽象类或接口时按类名字母序补第一个。再挂同组另一个会拆掉旧的。
        /// 当代码依赖一个抽象组件类型（比如 Collider2D），而实际需要挂载的是它的某个具体实现（BoxCollider2D、CircleCollider2D 等），且具体选哪个应该由使用者在编辑器里决定时，Unity 原生做不到自动挂载、互斥和切换——这个特性就是干这事的
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
