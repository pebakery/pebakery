/*
    Copyright (C) 2016-2020 Hajin Jang
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

namespace PEBakery.Tree
{
    #region class KwayTree
    public class KwayTree<T> : IEnumerable<T>
    {
        #region Fields and Properties
        private readonly List<int> _idList;

        public List<KwayTreeNode<T>> Root { get; }
        public int Count { get; private set; }
        #endregion

        #region Constructor
        public KwayTree()
        {
            Root = new List<KwayTreeNode<T>>();
            _idList = new List<int> { 0 };
            Count = 0;
        }
        #endregion

        #region Add, Delete, Count
        /// <summary>
        /// Add node to tree. Returns node id. If fails, return -1.
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public int AddNode(int parentId, T data)
        {
            if (parentId == 0)
            { // Root NodeList
                int id = _idList.Max() + 1;
                _idList.Add(id);
                KwayTreeNode<T> node = new KwayTreeNode<T>(parentId, id, data, null);
                Root.Add(node);
                Count++;
                return id;
            }
            else
            {
                int id = _idList.Max() + 1;
                _idList.Add(id);
                KwayTreeNode<T> parent = SearchNode(parentId);
                Debug.Assert(parent != null);

                KwayTreeNode<T> node = new KwayTreeNode<T>(parentId, id, data, parent.Child);
                parent.Child.Add(node);
                Count++;
                return id;
            }
        }

        /// <summary>
        /// Delete node from tree. If success, return true.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool DeleteNode(int id)
        {
            // Root NodeList, cannot delete
            if (id == 0)
                return false;

            KwayTreeNode<T> node = SearchNode(id, out List<KwayTreeNode<T>> sibling);
            Debug.Assert(node != null);
            Count -= CountLeaves(node);
            RecursiveDeleteNodeChild(node);
            sibling.Remove(node);
            Count--;
            return true;
        }

        private static void RecursiveDeleteNodeChild(KwayTreeNode<T> node)
        {
            foreach (KwayTreeNode<T> next in node.Child)
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
        public int CountLeaves(KwayTreeNode<T> node)
        {
            Queue<List<KwayTreeNode<T>>> q = new Queue<List<KwayTreeNode<T>>>();
            int leavesCount = 0;

            q.Enqueue(node.Child);
            while (0 < q.Count)
            {
                List<KwayTreeNode<T>> next = q.Dequeue();
                foreach (KwayTreeNode<T> leaf in next)
                {
                    leavesCount++;
                    if (0 < leaf.Child.Count)
                        q.Enqueue(leaf.Child);
                }
            }

            return leavesCount;
        }
        #endregion

        #region Search
        public KwayTreeNode<T> SearchNode(int id)
        {
            return id == 0 ? null : RecursiveSearchNode(id, Root, out _);
        }

        public KwayTreeNode<T> SearchNode(int id, out List<KwayTreeNode<T>> sibling)
        {
            if (id == 0)
            {
                sibling = null;
                return null;
            }

            // Start from root
            return RecursiveSearchNode(id, Root, out sibling);
        }

        private static KwayTreeNode<T> RecursiveSearchNode(int id, List<KwayTreeNode<T>> list, out List<KwayTreeNode<T>> sibling)
        {
            foreach (KwayTreeNode<T> node in list)
            {
                if (id == node.Id)
                {
                    sibling = list;
                    return node;
                }

                if (0 < node.Child.Count)
                {
                    KwayTreeNode<T> res = RecursiveSearchNode(id, node.Child, out sibling);
                    if (res != null)
                        return res;
                }
            }

            // Not found, return null
            sibling = null;
            return null;
        }

        public KwayTreeNode<T> SearchNode(T data)
        {
            return RecursiveSearchNode(data, Root, out _);
        }

        public KwayTreeNode<T> SearchNode(T data, out List<KwayTreeNode<T>> sibling)
        {
            return RecursiveSearchNode(data, Root, out sibling);
        }

        private static KwayTreeNode<T> RecursiveSearchNode(T data, List<KwayTreeNode<T>> list, out List<KwayTreeNode<T>> sibling)
        {
            foreach (KwayTreeNode<T> node in list)
            {
                if (data.Equals(node.Data))
                {
                    sibling = list;
                    return node;
                }

                if (0 < node.Child.Count)
                {
                    KwayTreeNode<T> res = RecursiveSearchNode(data, node.Child, out sibling);
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
            return SearchNode(id) != null;
        }

        public KwayTreeNode<T> GetNext(int id)
        {
            KwayTreeNode<T> node = SearchNode(id, out List<KwayTreeNode<T>> sibling);
            int idx = sibling.IndexOf(node);
            if (idx + 1 < sibling.Count)
                return sibling[idx + 1];
            else
                return null;
        }
        #endregion

        #region GetEnumerator (DFS, BFS)
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return GetEnumeratorDfs();
        }

        public IEnumerator<T> GetEnumeratorBfs()
        {
            Queue<List<KwayTreeNode<T>>> q = new Queue<List<KwayTreeNode<T>>>();
            Queue<KwayTreeNode<T>> qFinal = new Queue<KwayTreeNode<T>>();

            q.Enqueue(Root);

            while (0 < q.Count)
            {
                List<KwayTreeNode<T>> next = q.Dequeue();
                foreach (KwayTreeNode<T> node in next)
                {
                    qFinal.Enqueue(node);
                    if (0 < node.Child.Count)
                        q.Enqueue(node.Child);
                }
            }

            while (0 < qFinal.Count)
                yield return qFinal.Dequeue().Data;
        }

        public IEnumerator<T> GetEnumeratorDfs()
        {
            Queue<KwayTreeNode<T>> qFinal = new Queue<KwayTreeNode<T>>();
            RecursiveGetEnumeratorDfs(Root, qFinal);

            while (0 < qFinal.Count)
                yield return qFinal.Dequeue().Data;
        }

        private static void RecursiveGetEnumeratorDfs(List<KwayTreeNode<T>> list, Queue<KwayTreeNode<T>> qFinal)
        {
            foreach (KwayTreeNode<T> node in list)
            {
                qFinal.Enqueue(node);
                if (0 < node.Child.Count)
                    RecursiveGetEnumeratorDfs(node.Child, qFinal);
            }
        }
        #endregion

        #region Sort
        public void Sort(Comparison<KwayTreeNode<T>> sortFunc)
        {
            RecursiveSort(sortFunc, Root);
        }

        private static void RecursiveSort(Comparison<KwayTreeNode<T>> sortFunc, List<KwayTreeNode<T>> list)
        {
            list.Sort(sortFunc);

            foreach (KwayTreeNode<T> node in list.Where(node => 0 < node.Child.Count))
                RecursiveSort(sortFunc, node.Child);
        }
        #endregion
    }
    #endregion

    #region class KwayTreeNode
    public class KwayTreeNode<T>
    {
        public int Id;
        public int ParentId; // 0 is root NodeList
        public T Data;
        public List<KwayTreeNode<T>> Parent;
        public List<KwayTreeNode<T>> Child;

        public KwayTreeNode(int parentId, int id, T data, List<KwayTreeNode<T>> parent)
        {
            ParentId = parentId;
            Id = id;
            Data = data;
            Parent = parent;
            Child = new List<KwayTreeNode<T>>();
        }

        public override string ToString()
        {
            return $"Node ({Id}, {Data}, {Child.Count})";
        }
    }
    #endregion
}
