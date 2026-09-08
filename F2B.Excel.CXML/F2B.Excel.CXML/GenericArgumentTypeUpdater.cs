using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Linq;
using System.Windows.Threading;

namespace F2B.Excel.CXML
{
    public static class GenericArgumentTypeUpdater
    {
        private const string DisplayName = "DisplayName";

        public static void Attach(ModelItem modelItem)
        {
            Attach(modelItem, int.MaxValue);
        }

        public static void Attach(ModelItem modelItem, int maximumUpdatableTypes)
        {
            Type[] genericArguments = modelItem.ItemType.GetGenericArguments();
            if (!genericArguments.Any())
            {
                return;
            }

            int argumentCount = genericArguments.Length;
            int updatableArgumentCount = Math.Min(argumentCount, maximumUpdatableTypes);
            EditingContext context = modelItem.GetEditingContext();
            AttachedPropertiesService attachedPropertiesService = context.Services.GetService<AttachedPropertiesService>();

            for (int index = 0; index < updatableArgumentCount; index++)
            {
                AttachUpdatableArgumentType(modelItem, attachedPropertiesService, index, updatableArgumentCount);
            }
        }

        private static void AttachUpdatableArgumentType(
            ModelItem modelItem,
            AttachedPropertiesService attachedPropertiesService,
            int argumentIndex,
            int argumentCount)
        {
            string propertyName = "ArgumentType";
            if (argumentCount > 1)
            {
                propertyName += argumentIndex + 1;
            }

            var attachedProperty = new AttachedProperty<Type>
            {
                Name = propertyName,
                OwnerType = modelItem.ItemType,
                IsBrowsable = true
            };

            attachedProperty.Getter = arg => GetTypeArgument(arg, argumentIndex);
            attachedProperty.Setter = (arg, newType) => UpdateTypeArgument(arg, argumentIndex, newType);
            attachedPropertiesService.AddProperty(attachedProperty);
        }

        private static Type GetTypeArgument(ModelItem modelItem, int argumentIndex)
        {
            return modelItem.ItemType.GetGenericArguments()[argumentIndex];
        }

        private static void UpdateTypeArgument(ModelItem modelItem, int argumentIndex, Type newGenericType)
        {
            Type itemType = modelItem.ItemType;
            Type[] genericTypes = itemType.GetGenericArguments();
            genericTypes[argumentIndex] = newGenericType;
            Type newType = itemType.GetGenericTypeDefinition().MakeGenericType(genericTypes);
            EditingContext editingContext = modelItem.GetEditingContext();
            object instanceOfNewType = Activator.CreateInstance(newType);
            ModelItem newModelItem = ModelFactory.CreateItem(editingContext, instanceOfNewType);

            using (ModelEditingScope editingScope = newModelItem.BeginEdit("Change type argument"))
            {
                MorphHelper.MorphObject(modelItem, newModelItem);
                MorphHelper.MorphProperties(modelItem, newModelItem);

                if (itemType.IsSubclassOf(typeof(Activity)) && newType.IsSubclassOf(typeof(Activity)))
                {
                    string currentDisplayName = (string)modelItem.Properties[DisplayName].ComputedValue;
                    Activity temp = (Activity)Activator.CreateInstance(itemType);
                    string defaultName = temp.DisplayName;
                    if (string.IsNullOrWhiteSpace(currentDisplayName)
                        || string.Equals(
                            (currentDisplayName ?? string.Empty).Replace(" ", string.Empty),
                            (defaultName ?? string.Empty).Replace(" ", string.Empty),
                            StringComparison.Ordinal))
                    {
                        Activity created = (Activity)Activator.CreateInstance(newType);
                        newModelItem.Properties[DisplayName].SetValue(created.DisplayName);
                    }
                }

                DesignerUpdater.UpdateModelItem(modelItem, newModelItem);
                editingScope.Complete();
            }
        }

        private sealed class DesignerUpdater
        {
            private readonly ModelItem _originalModelItem;
            private readonly ModelItem _newModelItem;

            public DesignerUpdater(ModelItem originalItem, ModelItem newItem)
            {
                _originalModelItem = originalItem;
                _newModelItem = newItem;
            }

            public static void UpdateModelItem(ModelItem originalItem, ModelItem updatedItem)
            {
                var updater = new DesignerUpdater(originalItem, updatedItem);
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Render, new Action(updater.UpdateDesigner));
            }

            private void UpdateDesigner()
            {
                Selection.SelectOnly(_originalModelItem.GetEditingContext(), _newModelItem);
            }
        }
    }
}
