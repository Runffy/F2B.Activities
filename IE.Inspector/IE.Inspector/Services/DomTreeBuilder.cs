using System.Collections.Generic;
using System.Linq;
using F2B.Browser.IExplore.COM;
using IE.Inspector.Models;

namespace IE.Inspector.Services
{
    public static class DomTreeBuilder
    {
        public static DomTreeNode BuildDocumentTree(IEWindowController.IEDomElement target)
        {
            var path = BuildPathToDocumentRoot(target);
            if (path.Count == 0)
                return null;

            var rootElement = path[0];
            var rootNode = new DomTreeNode(rootElement, null) { IsExpanded = true };
            var currentNode = rootNode;

            for (var i = 1; i < path.Count; i++)
            {
                currentNode.LoadChildren();
                var nextElement = path[i];
                var childNode = currentNode.Children.FirstOrDefault(n => ElementsEqual(n.Element, nextElement));

                if (childNode == null)
                {
                    childNode = new DomTreeNode(nextElement, currentNode);
                    currentNode.Children.Add(childNode);
                }

                childNode.IsExpanded = true;
                currentNode = childNode;
            }

            currentNode.IsSelected = true;
            return rootNode;
        }

        public static DomTreeNode FindNode(DomTreeNode root, IEWindowController.IEDomElement element)
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

        public static IList<IEWindowController.IEDomElement> BuildPathToDocumentRoot(IEWindowController.IEDomElement target)
        {
            var path = new List<IEWindowController.IEDomElement>();
            var current = target;
            while (current != null)
            {
                path.Insert(0, current);
                current = current.get_parent();
            }

            return path;
        }

        private static bool ElementsEqual(IEWindowController.IEDomElement left, IEWindowController.IEDomElement right)
        {
            if (left == null || right == null)
                return false;

            return ReferenceEquals(left.raw, right.raw);
        }
    }
}
