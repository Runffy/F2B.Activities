using System;
using System.Activities;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace F2B.Basic
{
    /// <summary>
    /// Legacy non-generic Get Global Value. Kept for existing workflows; prefer <see cref="GetGlobalValueActivity{TResult}"/>.
    /// </summary>
    [Obsolete("Use GetGlobalValueActivity<T> (toolbox: Get Global Value) instead. This legacy activity remains for existing workflows.")]
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Global Container")]
    [DisplayName("Get Global Value (Legacy)")]
    [Description("Legacy: read a value from F2B.Global as object. Prefer Get Global Value (typed ArgumentType). Expression: F2B.Global.Get(\"keyname\")")]
    public sealed class GetGlobalValueActivity : CodeActivity<object>, System.Activities.Presentation.IActivityTemplateFactory
    {
        public GetGlobalValueActivity()
        {
            DisplayName = "Get Global Value (Legacy)";
        }

        [RequiredArgument]
        [DisplayName("Key")]
        [Category("Input.A")]
        public InArgument<string> Key { get; set; }

        [DisplayName("Value")]
        [Category("Output")]
        public OutArgument<object> Value { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new GetGlobalValueActivity();
        }

        protected override object Execute(CodeActivityContext context)
        {
            object value = Global.Get(Key.Get(context));
            Value?.Set(context, value);
            return value;
        }
    }

    [Designer(typeof(BasicGenericActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Global Container")]
    [DisplayName("Get Global Value")]
    [Description("Read a value from F2B.Global. Change ArgumentType to set Result type. Expression: F2B.Global.Get(\"keyname\")")]
    [System.Activities.Presentation.DefaultTypeArgument(typeof(object))]
    public sealed class GetGlobalValueActivity<TResult> : CodeActivity<TResult>
    {
        public GetGlobalValueActivity()
        {
            DisplayName = "Get Global Value";
        }

        [RequiredArgument]
        [DisplayName("Key")]
        [Category("Input.A")]
        public InArgument<string> Key { get; set; }

        protected override TResult Execute(CodeActivityContext context)
        {
            object value = Global.Get(Key.Get(context));
            return ConvertGlobalValueTo(value);
        }

        private static TResult ConvertGlobalValueTo(object raw)
        {
            Type target = typeof(TResult);
            Type underlying = Nullable.GetUnderlyingType(target) ?? target;

            if (raw == null || raw is DBNull || (raw is JToken jtNull && jtNull.Type == JTokenType.Null))
            {
                if (target.IsValueType && Nullable.GetUnderlyingType(target) == null)
                {
                    throw new InvalidCastException(
                        "Cannot convert null Global value to non-nullable type '" + target.FullName + "'.");
                }

                return default(TResult);
            }

            if (raw is JToken token)
            {
                try
                {
                    return token.ToObject<TResult>();
                }
                catch (Exception ex)
                {
                    throw new InvalidCastException(
                        "Cannot convert Global JSON token of type '" + token.Type
                        + "' to '" + target.FullName + "'.",
                        ex);
                }
            }

            if (raw is TResult typed)
            {
                return typed;
            }

            try
            {
                if (underlying.IsEnum)
                {
                    if (raw is string enumText)
                    {
                        return (TResult)Enum.Parse(underlying, enumText, ignoreCase: true);
                    }

                    return (TResult)Enum.ToObject(underlying, raw);
                }

                if (underlying == typeof(Guid))
                {
                    return (TResult)(object)Guid.Parse(Convert.ToString(raw, CultureInfo.InvariantCulture));
                }

                if (underlying == typeof(string))
                {
                    return (TResult)(object)Convert.ToString(raw, CultureInfo.InvariantCulture);
                }

                object converted = Convert.ChangeType(raw, underlying, CultureInfo.InvariantCulture);
                return (TResult)converted;
            }
            catch (Exception ex)
            {
                throw new InvalidCastException(
                    "Cannot convert Global value of type '" + raw.GetType().FullName
                    + "' to '" + target.FullName + "'.",
                    ex);
            }
        }
    }

    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Global Container")]
    [DisplayName("Set Global Value")]
    [Description("Write a value into F2B.Global. Expression: F2B.Global.Set(\"keyname\", value)")]
    public sealed class SetGlobalValueActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public SetGlobalValueActivity()
        {
            DisplayName = "Set Global Value";
        }

        [RequiredArgument]
        [DisplayName("Key")]
        [Category("Input.A")]
        public InArgument<string> Key { get; set; }

        [RequiredArgument]
        [DisplayName("Value")]
        [Category("Input.A")]
        public InArgument<object> Value { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new SetGlobalValueActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            Global.Set(Key.Get(context), Value.Get(context));
        }
    }

    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Global Container")]
    [DisplayName("Clear Global Value")]
    [Description("Clear the entire F2B.Global dictionary. Expression: F2B.Global.Clear()")]
    public sealed class ClearGlobalValueActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public ClearGlobalValueActivity()
        {
            DisplayName = "Clear Global Value";
        }

        public Activity Create(DependencyObject target)
        {
            return new ClearGlobalValueActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            Global.Clear();
        }
    }
}
