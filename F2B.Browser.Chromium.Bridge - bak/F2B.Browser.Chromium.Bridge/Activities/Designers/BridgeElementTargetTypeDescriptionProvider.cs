using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    public interface IBridgeElementTargetConfig
    {
        BridgeElementTargetType TargetType { get; }
    }

    public sealed class BridgeElementTargetTypeDescriptionProvider : TypeDescriptionProvider
    {
        private static readonly TypeDescriptionProvider DefaultProvider =
            TypeDescriptor.GetProvider(typeof(CodeActivity));

        public BridgeElementTargetTypeDescriptionProvider() : base(DefaultProvider)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            var baseDescriptor = base.GetTypeDescriptor(objectType, instance);
            return new BridgeElementTargetTypeDescriptor(baseDescriptor, instance as IBridgeElementTargetConfig);
        }

        private sealed class BridgeElementTargetTypeDescriptor : CustomTypeDescriptor
        {
            private readonly IBridgeElementTargetConfig _config;

            public BridgeElementTargetTypeDescriptor(ICustomTypeDescriptor parent, IBridgeElementTargetConfig config)
                : base(parent)
            {
                _config = config;
            }

            public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
            {
                var original = base.GetProperties(attributes);
                var filtered = new PropertyDescriptor[original.Count];
                for (var i = 0; i < original.Count; i++)
                    filtered[i] = new BridgeElementTargetPropertyDescriptor(original[i], _config);

                return new PropertyDescriptorCollection(filtered, true);
            }
        }

        private sealed class BridgeElementTargetPropertyDescriptor : PropertyDescriptor
        {
            private readonly PropertyDescriptor _inner;
            private readonly IBridgeElementTargetConfig _config;

            public BridgeElementTargetPropertyDescriptor(PropertyDescriptor inner, IBridgeElementTargetConfig config)
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
                        if (_config.TargetType == BridgeElementTargetType.Element &&
                            (Name == "InputTab" || Name == "Selector"))
                        {
                            return false;
                        }

                        if (_config.TargetType == BridgeElementTargetType.Selector && Name == "Element")
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
