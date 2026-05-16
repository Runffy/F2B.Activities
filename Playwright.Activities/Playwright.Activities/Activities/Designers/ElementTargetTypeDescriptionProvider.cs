using System;
using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    public interface IElementTargetConfig
    {
        ElementTargetType TargetType { get; }
    }

    public sealed class ElementTargetTypeDescriptionProvider : TypeDescriptionProvider
    {
        private static readonly TypeDescriptionProvider DefaultProvider =
            TypeDescriptor.GetProvider(typeof(CodeActivity));

        public ElementTargetTypeDescriptionProvider() : base(DefaultProvider)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            var baseDescriptor = base.GetTypeDescriptor(objectType, instance);
            return new ElementTargetTypeDescriptor(baseDescriptor, instance as IElementTargetConfig);
        }

        private sealed class ElementTargetTypeDescriptor : CustomTypeDescriptor
        {
            private readonly IElementTargetConfig _config;

            public ElementTargetTypeDescriptor(ICustomTypeDescriptor parent, IElementTargetConfig config)
                : base(parent)
            {
                _config = config;
            }

            public override PropertyDescriptorCollection GetProperties()
            {
                return GetProperties(new Attribute[0]);
            }

            public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
            {
                var original = base.GetProperties(attributes);
                var filtered = new PropertyDescriptor[original.Count];
                for (var i = 0; i < original.Count; i++)
                {
                    filtered[i] = new ElementTargetPropertyDescriptor(original[i], _config);
                }

                return new PropertyDescriptorCollection(filtered, true);
            }
        }

        private sealed class ElementTargetPropertyDescriptor : PropertyDescriptor
        {
            private readonly PropertyDescriptor _inner;
            private readonly IElementTargetConfig _config;

            public ElementTargetPropertyDescriptor(PropertyDescriptor inner, IElementTargetConfig config)
                : base(inner)
            {
                _inner = inner;
                _config = config;
            }

            public override bool IsBrowsable
            {
                get
                {
                    if (_config != null)
                    {
                        if (_config.TargetType == ElementTargetType.Element &&
                            (Name == "InputTab" || Name == "Selector"))
                        {
                            return false;
                        }

                        if (_config.TargetType == ElementTargetType.Selector && Name == "Element")
                        {
                            return false;
                        }
                    }

                    return _inner.IsBrowsable;
                }
            }

            public override Type ComponentType => _inner.ComponentType;
            public override bool IsReadOnly => _inner.IsReadOnly;
            public override Type PropertyType => _inner.PropertyType;
            public override bool CanResetValue(object component) => _inner.CanResetValue(component);
            public override object GetValue(object component) => _inner.GetValue(component);
            public override void ResetValue(object component) => _inner.ResetValue(component);
            public override void SetValue(object component, object value) => _inner.SetValue(component, value);
            public override bool ShouldSerializeValue(object component) => _inner.ShouldSerializeValue(component);
        }
    }
}
