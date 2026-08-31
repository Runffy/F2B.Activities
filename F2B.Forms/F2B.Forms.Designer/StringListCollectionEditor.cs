using System;
using System.ComponentModel.Design;

namespace F2B.Forms.Designer
{
    /// <summary>
    /// CollectionEditor for <see cref="System.Collections.Generic.List{T}"/> of strings.
    /// Default editor calls Activator.CreateInstance(typeof(string)), which fails because
    /// System.String has no parameterless constructor.
    /// </summary>
    public sealed class StringListCollectionEditor : CollectionEditor
    {
        public StringListCollectionEditor(Type type)
            : base(type)
        {
        }

        protected override Type CreateCollectionItemType()
        {
            return typeof(string);
        }

        protected override object CreateInstance(Type itemType)
        {
            if (itemType == typeof(string))
            {
                return string.Empty;
            }

            return base.CreateInstance(itemType);
        }
    }
}
