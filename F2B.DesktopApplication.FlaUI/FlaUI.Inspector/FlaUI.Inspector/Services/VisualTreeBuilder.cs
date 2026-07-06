using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Inspector.Models;

namespace FlaUI.Inspector.Services
{
    public static class VisualTreeBuilder
    {
        public static VisualTreeNode BuildWindowTree(AutomationElement target)
        {
            var path = SelectorBuilder.BuildPathToRoot(target);
            if (path.Count == 0)
                return null;

            var rootElement = path[0];
            var rootNode = new VisualTreeNode(rootElement, null) { IsExpanded = true };
            var currentNode = rootNode;
            var currentElement = rootElement;

            for (var i = 1; i < path.Count; i++)
            {
                currentNode.LoadChildren();
                var nextElement = path[i];
                var childNode = currentNode.Children.FirstOrDefault(n => ElementsEqual(n.Element, nextElement));

                if (childNode == null)
                {
                    childNode = new VisualTreeNode(nextElement, currentNode);
                    currentNode.Children.Add(childNode);
                }

                childNode.IsExpanded = true;
                currentNode = childNode;
                currentElement = nextElement;
            }

            currentNode.IsSelected = true;
            return rootNode;
        }

        public static VisualTreeNode FindNode(VisualTreeNode root, AutomationElement element)
        {
            if (root == null || element == null)
                return null;

            if (ElementsEqual(root.Element, element))
                return root;

            root.LoadChildren();
            foreach (var child in root.Children)
            {
                var found = FindNode(child, element);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool ElementsEqual(AutomationElement left, AutomationElement right)
        {
            if (left == null || right == null)
                return false;

            try
            {
                return left.Equals(right);
            }
            catch
            {
                return ReferenceEquals(left, right);
            }
        }
    }
}
