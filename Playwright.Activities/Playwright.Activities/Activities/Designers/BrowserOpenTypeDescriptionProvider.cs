using System;
using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    public interface IBrowserOpenConfig
    {
        bool UseSystemDir { get; }
    }

    public sealed class BrowserOpenTypeDescriptionProvider : TypeDescriptionProvider
    {
        private static readonly TypeDescriptionProvider DefaultProvider =
            TypeDescriptor.GetProvider(typeof(CodeActivity));

        public BrowserOpenTypeDescriptionProvider() : base(DefaultProvider)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            var baseDescriptor = base.GetTypeDescriptor(objectType, instance);
            return new BrowserOpenTypeDescriptor(baseDescriptor, instance as IBrowserOpenConfig);
        }

        private sealed class BrowserOpenTypeDescriptor : CustomTypeDescriptor
        {
            private readonly IBrowserOpenConfig _config;

            public BrowserOpenTypeDescriptor(ICustomTypeDescriptor parent, IBrowserOpenConfig config)
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
                    filtered[i] = new BrowserOpenPropertyDescriptor(original[i], _config);
                }

                return new PropertyDescriptorCollection(filtered, true);
            }
        }

        private sealed class BrowserOpenPropertyDescriptor : PropertyDescriptor
        {
            private readonly PropertyDescriptor _inner;
            private readonly IBrowserOpenConfig _config;

            public BrowserOpenPropertyDescriptor(PropertyDescriptor inner, IBrowserOpenConfig config)
                : base(inner)
            {
                _inner = inner;
                _config = config;
            }

            public override bool IsBrowsable
            {
                get
                {
                    if (_config != null && _config.UseSystemDir && Name == "UserDataDir")
                    {
                        return false;
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
