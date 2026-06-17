using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeTakeScreenshotTypeDescriptionProvider : TypeDescriptionProvider
    {
        private static readonly TypeDescriptionProvider DefaultProvider =
            TypeDescriptor.GetProvider(typeof(CodeActivity));

        public BridgeTakeScreenshotTypeDescriptionProvider() : base(DefaultProvider)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            var baseDescriptor = base.GetTypeDescriptor(objectType, instance);
            return new TakeScreenshotTypeDescriptor(baseDescriptor, instance);
        }

        private sealed class TakeScreenshotTypeDescriptor : CustomTypeDescriptor
        {
            private readonly object _instance;

            public TakeScreenshotTypeDescriptor(ICustomTypeDescriptor parent, object instance)
                : base(parent)
            {
                _instance = instance;
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
                    filtered[i] = new TakeScreenshotPropertyDescriptor(original[i], _instance);
                }

                return new PropertyDescriptorCollection(filtered, true);
            }
        }

        private sealed class TakeScreenshotPropertyDescriptor : PropertyDescriptor
        {
            private readonly PropertyDescriptor _inner;
            private readonly object _instance;

            public TakeScreenshotPropertyDescriptor(PropertyDescriptor inner, object instance)
                : base(inner)
            {
                _inner = inner;
                _instance = instance;
            }

            public override bool IsBrowsable
            {
                get
                {
                    var screenshotConfig = _instance as IBridgeTakeScreenshotConfig;
                    if (screenshotConfig != null)
                    {
                        if (screenshotConfig.BaseOn != BridgeTakeScreenshotBaseOn.Tab &&
                            Name == "FullPage")
                        {
                            return false;
                        }

                        if (screenshotConfig.BaseOn == BridgeTakeScreenshotBaseOn.Tab &&
                            (Name == "TargetType" ||
                             Name == "Selector" ||
                             Name == "InputElement" ||
                             Name == "Timeout" ||
                             Name == "DelayBefore"))
                        {
                            return false;
                        }
                    }

                    var targetConfig = _instance as IBridgeElementTargetConfig;
                    if (targetConfig != null &&
                        screenshotConfig != null &&
                        screenshotConfig.BaseOn == BridgeTakeScreenshotBaseOn.Element)
                    {
                        if (targetConfig.TargetType == BridgeElementTargetType.Element &&
                            (Name == "InputTab" || Name == "Selector"))
                        {
                            return false;
                        }

                        if (targetConfig.TargetType == BridgeElementTargetType.Selector && Name == "InputElement")
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
