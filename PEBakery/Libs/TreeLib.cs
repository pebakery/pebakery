/*
    Copyright (C) 2016-2017 Hajin Jang
    Licensed under MIT License.
 
    MIT License

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PEBakery.Lib
{
    public class Tree<T> : IEnumerable
    {
        private List<Node<T>> root;
        private List<int> idList;
        private int count;

        public List<Node<T>> Root { get => root; }
        public int Count { get => count; }

        public Tree()
        {
            root = new List<Node<T>>();
            idList = new List<int>();
            idList.Add(0);
            count = 0;
        }

        /// <summary>
        /// Add node to tree. Returns node id. If fails, return -1.
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="id"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public int AddNode(int parentId, T data)
        {
            if (parentId == 0)
            { // Root NodeList
                int id = idList.Max() + 1;
                idList.Add(id);
                Node<T> node = new Node<T>(parentId, id, data, null);
                root.Add(node);
                count++;
                return id;
            }
            else
            {
                int id = idList.Max() + 1;
                idList.Add(id);
                Node<T> parent = SearchNode(parentId);
                Debug.Assert(parent != null);
                if (parent == null)
                    return -1;
                Node<T> node = new Node<T>(parentId, id, data, parent.Child);
                parent.Child.Add(node);
                count++;
                return id;
            }
        }

        /// <summary>
        /// Delete node from tree. If success, return true.
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="id"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool DeleteNode(int id)
        {
            if (id == 0)
            { // Root NodeList, cannot delete
                return false;
            }
            else
            {
                List<Node<T>> sibling = new List<Node<T>>();
                Node<T> node = SearchNode(id, out sibling);
                Debug.Assert(node != null);
                if (node == null)
                    return false;
                count -= CountLeaves(node);
                RecursiveDeleteNodeChild(node);
                sibling.Remove(node);
                node = null;
                count--;
                return true;
            }
        }

        private void RecursiveDeleteNodeChild(Node<T> node)
        {
            foreach (Node<T> next in node.Child)
            {
                RecursiveDeleteNodeChild(next);
                next.Child = null;
            }
        }

        /// <summary>
        /// Do not include root node itself.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public int CountLeaves(Node<T> node)
        {
            Queue<List<Node<T>>> q = new Queue<List<Node<T>>>();
            int leavesCount = 0;

            q.Enqueue(node.Child);
            while (0 < q.Count)
            {
                List<Node<T>> next = q.Dequeue();
                foreach (Node<T> leaf in next)
                {
                    leavesCount++;
                    if (0 < leaf.Child.Count)
                        q.Enqueue(leaf.Child);
                }
            }

            return leavesCount;
        }

        public Node<T> SearchNode(int id)
        {
            if (id == 0)
            {
                return null;
            }
            else
            { // Start from root
                List<Node<T>> dummy;
                return RecursiveSearchNode(id, root, out dummy);
            }
        }

        public Node<T> SearchNode(int id, out List<Node<T>> sibling)
        {
            if (id == 0)
            {
                sibling = null;
                return null;
            }
            else
            { // Start from root
                return RecursiveSearchNode(id, root, out sibling);
            }
        }

        private Node<T> RecursiveSearchNode(int id, List<Node<T>> list, out List<Node<T>> sibling)
        {
            foreach (Node<T> node in list)
            {
                if (id == node.Id)
                {
                    sibling = list;
                    return node;
                }

                if (0 < node.Child.Count)
                {
                    Node<T> res = RecursiveSearchNode(id, node.Child, out sibling);
                    if (res != null)
                        return res;
                }
            }

            // Not found, return null
            sibling = null;
            return null;
        }

        public Node<T> SearchNode(T data)
        {
            return RecursiveSearchNode(data, root, out List<Node<T>> dummy);
        }

        public Node<T> SearchNode(T data, out List<Node<T>> sibling)
        {
            return RecursiveSearchNode(data, root, out sibling);
        }

        private Node<T> RecursiveSearchNode(T data, List<Node<T>> list, out List<Node<T>> sibling)
        {
            foreach (Node<T> node in list)
            {
                if (data.Equals(node.Data))
                {
                    sibling = list;
                    return node;
                }

                if (0 < node.Child.Count)
                {
                    Node<T> res = RecursiveSearchNode(data, node.Child, out sibling);
                    if (res != null)
                        return res;
                }
            }

            // Not found, return null
            sibling = null;
            return null;
        }

        public bool Contains(int id)
        {
            if (SearchNode(id) == null)
                return false;
            else
                return true;
        }

        public Node<T> GetNext(int id)
        {
            List<Node<T>> sibling;
            Node<T> node = SearchNode(id, out sibling);
            int idx = sibling.IndexOf(node);
            if (idx + 1 < sibling.Count)
                return sibling[idx + 1];
            else
                return null;
        }

        public IEnumerator GetEnumerator()
        {
            return GetEnumeratorDFS();
        }

        public IEnumerator GetEnumeratorBFS()
        {
            Queue<List<Node<T>>> q = new Queue<List<Node<T>>>();
            Queue<Node<T>> qFinal = new Queue<Node<T>>();

            q.Enqueue(root);

            while (0 < q.Count)
            {
                List<Node<T>> next = q.Dequeue();
                foreach (Node<T> node in next)
                {
                    qFinal.Enqueue(node);
                    if (0 < node.Child.Count)
                        q.Enqueue(node.Child);
                }
            }
            
            while (0 < qFinal.Count)
                yield return qFinal.Dequeue().Data;
        }

        public IEnumerator GetEnumeratorDFS()
        { // Not tested
            Queue<Node<T>> qFinal = new Queue<Node<T>>();
            RecursiveGetEnumeratorDFS(root, qFinal);

            while (0 < qFinal.Count)
                yield return qFinal.Dequeue().Data;
        }

        private void RecursiveGetEnumeratorDFS(List<Node<T>> list, Queue<Node<T>> qFinal)
        {
            foreach (Node<T> node in list)
            {
                qFinal.Enqueue(node);
                if (0 < node.Child.Count)
                    RecursiveGetEnumeratorDFS(node.Child, qFinal);
            }
        }

        public void Sort(Comparison<Node<T>> sortFunc)
        {
            RecursiveSort(sortFunc, root);
        }

        private void RecursiveSort(Comparison<Node<T>> sortFunc, List<Node<T>> list)
        {
            list.Sort(sortFunc);

            foreach (Node<T> node in list)
            {
                if (0 < node.Child.Count)
                    RecursiveSort(sortFunc, node.Child);
            }
        }

    }

    public class Node<T>
    {
        public int Id;
        public int ParentId; // 0 is root NodeList
        public T Data;
        public List<Node<T>> Parent;
        public List<Node<T>> Child;

        public Node(int parentId, int id, T data, List<Node<T>> parent)
        {
            this.ParentId = parentId;
            this.Id = id;
            this.Data = data;
            this.Parent = parent;
            this.Child = new List<Node<T>>();
        }

        public override string ToString()
        {
            return $"Node ({Id}, {Data}, {Child.Count})";
        }
    }
}
