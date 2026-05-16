using System;
using System.Activities;
using System.ComponentModel;

namespace Playwright.Activities
{
    public interface IClickValidationConfig
    {
        ClickValidateMode Validate { get; }
    }

    public sealed class ClickValidationTypeDescriptionProvider : TypeDescriptionProvider
    {
        private static readonly TypeDescriptionProvider DefaultProvider =
            TypeDescriptor.GetProvider(typeof(CodeActivity));

        public ClickValidationTypeDescriptionProvider() : base(DefaultProvider)
        {
        }

        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            var baseDescriptor = base.GetTypeDescriptor(objectType, instance);
            return new ClickValidationTypeDescriptor(baseDescriptor, instance);
        }

        private sealed class ClickValidationTypeDescriptor : CustomTypeDescriptor
        {
            private readonly object _instance;

            public ClickValidationTypeDescriptor(ICustomTypeDescriptor parent, object instance)
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
                    filtered[i] = new ClickValidationPropertyDescriptor(original[i], _instance);
                }

                return new PropertyDescriptorCollection(filtered, true);
            }
        }

        private sealed class ClickValidationPropertyDescriptor : PropertyDescriptor
        {
            private readonly PropertyDescriptor _inner;
            private readonly object _instance;

            public ClickValidationPropertyDescriptor(PropertyDescriptor inner, object instance)
                : base(inner)
            {
                _inner = inner;
                _instance = instance;
            }

            public override bool IsBrowsable
            {
                get
                {
                    var clickConfig = _instance as IClickValidationConfig;
                    if (clickConfig != null &&
                        clickConfig.Validate == ClickValidateMode.None &&
                        (Name == "ValidationSelector" || Name == "Interval"))
                    {
                        return false;
                    }

                    var targetConfig = _instance as IElementTargetConfig;
                    if (targetConfig != null)
                    {
                        if (targetConfig.TargetType == ElementTargetType.Element &&
                            (Name == "InputTab" || Name == "Selector"))
                        {
                            return false;
                        }

                        if (targetConfig.TargetType == ElementTargetType.Selector && Name == "Element")
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
