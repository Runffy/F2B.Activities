using System;
using System.Activities;
using System.ComponentModel;

namespace F2B.DesktopApplication.FlaUI
{
    public interface IFlaUiElementTargetConfig
    {
        ElementTargetType TargetType { get; }
    }

    public sealed class FlaUiElementTargetTypeDescriptionProvider : TypeDescriptionProvider
    {
        private static readonly TypeDescriptionProvider DefaultProvider =
            TypeDescriptor.GetProvider(typeof(CodeActivity));

        public FlaUiElementTargetTypeDescriptionProvider()
            : base(DefaultProvider)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            var baseDescriptor = base.GetTypeDescriptor(objectType, instance);
            return new FlaUiElementTargetTypeDescriptor(baseDescriptor, instance as IFlaUiElementTargetConfig);
        }

        private sealed class FlaUiElementTargetTypeDescriptor : CustomTypeDescriptor
        {
            private readonly IFlaUiElementTargetConfig _config;

            public FlaUiElementTargetTypeDescriptor(ICustomTypeDescriptor parent, IFlaUiElementTargetConfig config)
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
                    filtered[i] = new FlaUiElementTargetPropertyDescriptor(original[i], _config);

                return new PropertyDescriptorCollection(filtered, true);
            }
        }

        private sealed class FlaUiElementTargetPropertyDescriptor : PropertyDescriptor
        {
            private readonly PropertyDescriptor _inner;
            private readonly IFlaUiElementTargetConfig _config;

            public FlaUiElementTargetPropertyDescriptor(PropertyDescriptor inner, IFlaUiElementTargetConfig config)
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
                            (Name == "InputWindow" || Name == "Selector"))
                            return false;

                        if (_config.TargetType == ElementTargetType.Selector && Name == "Element")
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
                if (component is FlaUiElementTargetActivityBase activity)
                {
                    switch (Name)
                    {
                        case "Element":
                            return ActivityArgumentHelper.HasExpression(activity.Element);
                        case "Selector":
                            return ActivityArgumentHelper.HasExpression(activity.Selector);
                        case "InputWindow":
                            return ActivityArgumentHelper.HasExpression(activity.InputWindow);
                        case "TargetType":
                            return activity.TargetType != ElementTargetType.Selector;
                    }
                }

                return _inner.ShouldSerializeValue(component);
            }
        }
    }
}
