using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;

namespace Playwright.Activities
{
    [TypeDescriptionProvider(typeof(ElementTargetTypeDescriptionProvider))]
    [DisplayName("Element.SelectGetSelected")]
    public sealed class ElementSelectGetSelectedActivity : ElementTargetActivityBase
    {
        [Category("Output")]
        public OutArgument<List<Dictionary<string, object>>> Selected { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            Selected?.Set(context, ResolveTargetElement(context).SelectGetSelected());
        }
    }
}
