using System;

namespace TechCosmos.RequireOneOf
{
    /// <summary>
    /// 解决 Unity 原生 [RequireComponent] 的局限：无法声明"这组组件必须有一个且只能有一个"。
    /// 声明后，编辑器自动保证：缺失补挂、重复删除、Inspector 一键切换并迁移相同字段值。
    /// 这个包的解决方案
    /// 通过 [RequireOneOf(typeof(A), typeof(B))]：
    /// Inspector 里一键切换具体实现，自动迁移可对应的字段
    /// 预制体可以保存不同的选择——圆形的预制体选 CircleCollider2D，方形的选 BoxCollider2D
    /// 代码层面拿到的永远是 Collider2D，不用关心具体类型(Collider2d在这里只是一个例子,这个特性解决的是通用问题)
    /// 这才是它存在的意义：让"抽象能力"和"具体实现"在预制体层面可以灵活配置，而不需要改代码或做复杂的变体管理
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
