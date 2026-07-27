using System;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    /// <summary>
    /// Renames the inherited Selector property display name to "Child Selector" for GetChildren.
    /// </summary>
    internal sealed class ElementGetChildrenTypeDescriptionProvider : TypeDescriptionProvider
    {
        private static readonly TypeDescriptionProvider DefaultProvider =
            TypeDescriptor.GetProvider(typeof(CdpElementTargetActivityBase));

        public ElementGetChildrenTypeDescriptionProvider()
            : base(DefaultProvider)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            return new Descriptor(base.GetTypeDescriptor(objectType, instance));
        }

        private sealed class Descriptor : CustomTypeDescriptor
        {
            public Descriptor(ICustomTypeDescriptor parent)
                : base(parent)
            {
            }

            public override PropertyDescriptorCollection GetProperties()
            {
                return Wrap(base.GetProperties());
            }

            public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
            {
                return Wrap(base.GetProperties(attributes));
            }

            private static PropertyDescriptorCollection Wrap(PropertyDescriptorCollection original)
            {
                var wrapped = new PropertyDescriptor[original.Count];
                for (var i = 0; i < original.Count; i++)
                {
                    var property = original[i];
                    if (string.Equals(property.Name, "Selector", StringComparison.Ordinal))
                    {
                        wrapped[i] = TypeDescriptor.CreateProperty(
                            property.ComponentType,
                            property,
                            new DisplayNameAttribute("Child Selector"),
                            new DescriptionAttribute(
                                "Optional selector that filters child elements under Target. Direct children by default; Deepdive searches descendants."));
                    }
                    else
                    {
                        wrapped[i] = property;
                    }
                }

                return new PropertyDescriptorCollection(wrapped, true);
            }
        }
    }
}
