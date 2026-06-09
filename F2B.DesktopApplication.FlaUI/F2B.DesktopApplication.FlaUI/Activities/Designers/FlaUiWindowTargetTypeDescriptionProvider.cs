using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    public interface IFlaUiWindowTargetConfig
    {
        WindowTargetType TargetType { get; }
    }

    public sealed class FlaUiWindowTargetTypeDescriptionProvider : TypeDescriptionProvider
    {
        private static readonly TypeDescriptionProvider DefaultProvider =
            TypeDescriptor.GetProvider(typeof(CodeActivity));

        public FlaUiWindowTargetTypeDescriptionProvider()
            : base(DefaultProvider)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            var baseDescriptor = base.GetTypeDescriptor(objectType, instance);
            return new FlaUiWindowTargetTypeDescriptor(baseDescriptor, instance as IFlaUiWindowTargetConfig);
        }

        private sealed class FlaUiWindowTargetTypeDescriptor : CustomTypeDescriptor
        {
            private readonly IFlaUiWindowTargetConfig _config;

            public FlaUiWindowTargetTypeDescriptor(ICustomTypeDescriptor parent, IFlaUiWindowTargetConfig config)
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
                    filtered[i] = new FlaUiWindowTargetPropertyDescriptor(original[i], _config);

                return new PropertyDescriptorCollection(filtered, true);
            }
        }

        private sealed class FlaUiWindowTargetPropertyDescriptor : PropertyDescriptor
        {
            private readonly PropertyDescriptor _inner;
            private readonly IFlaUiWindowTargetConfig _config;

            public FlaUiWindowTargetPropertyDescriptor(PropertyDescriptor inner, IFlaUiWindowTargetConfig config)
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
                        if (_config.TargetType == WindowTargetType.Window && Name == "Selector")
                            return false;

                        if (_config.TargetType == WindowTargetType.Selector && Name == "InputWindow")
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

            public override bool ShouldSerializeValue(object component)
            {
                if (component is FlaUiWindowTargetActivityBase activity)
                {
                    switch (Name)
                    {
                        case "InputWindow":
                            return ActivityArgumentHelper.HasExpression(activity.InputWindow);
                        case "Selector":
                            return ActivityArgumentHelper.HasExpression(activity.Selector);
                        case "TargetType":
                            return activity.TargetType != WindowTargetType.Selector;
                    }
                }

                return _inner.ShouldSerializeValue(component);
            }
        }
    }
}
