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
                Node<T> node = new Node<T>(parentId, id, data, root);
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
        /// Retrieve node from tree. Alias of SearchDFS.
        /// </summary>
        /// <param name="id">Node Id</param>
        /// <returns></returns>
        public Node<T> GetNode(int id)
        {
            return SearchNode(id);
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

    public class ConcurrentTree<T> : IEnumerable
    {
        private List<ConcurrentNode<T>> root;
        private int count;
        private ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();

        public int Count { get => count; }

        public ConcurrentTree()
        {
            cacheLock.EnterWriteLock();
            try
            {
                root = new List<ConcurrentNode<T>>();
                count = 0;
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Add ConcurrentNode to tree. If success, return true.
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="id"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool AddNode(int parentId, int id, T data)
        {
            if (parentId == 0)
            { // Root ConcurrentNodeList
                ConcurrentNode<T> ConcurrentNode = new ConcurrentNode<T>(parentId, id, data, root);
                cacheLock.EnterWriteLock();
                try
                {
                    root.Add(ConcurrentNode);
                    count++;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }
                return true;
            }
            else
            {
                ConcurrentNode<T> parent = SearchNode(parentId);
                ConcurrentNode<T> node;
                cacheLock.EnterReadLock();
                try
                {
                    node = new ConcurrentNode<T>(parentId, id, data, parent.Child);
                    if (parent == null)
                        return false;
                }
                finally
                {
                    cacheLock.ExitReadLock();
                }
                cacheLock.EnterWriteLock();
                try
                {
                    parent.Child.Add(node);
                    count++;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }
                return true;
            }
        }

        /// <summary>
        /// Retrieve ConcurrentNode from tree. Alias of SearchDFS.
        /// </summary>
        /// <param name="id">ConcurrentNode Id</param>
        /// <returns></returns>
        public ConcurrentNode<T> GetConcurrentNode(int id)
        {
            return SearchNode(id);
        }

        /// <summary>
        /// Delete ConcurrentNode from tree. If success, return true.
        /// </summary>
        /// <param name="parentId"></param>
        /// <param name="id"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool DeleteNode(int id)
        {
            if (id == 0)
            { // Root ConcurrentNodeList, cannot delete
                return false;
            }
            else
            {
                List<ConcurrentNode<T>> sibling = new List<ConcurrentNode<T>>();
                ConcurrentNode<T> node = SearchNode(id, out sibling);
                cacheLock.EnterReadLock();
                try
                {
                    if (node == null)
                        return false;
                }
                finally
                {
                    cacheLock.ExitReadLock();
                }
                RecursiveDeleteConcurrentNodeChild(node);
                cacheLock.EnterWriteLock();
                try
                {
                    count -= CountLeaves(node);
                    sibling.Remove(node);
                    node = null;
                    count--;
                    return true;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }
            }
        }

        private void RecursiveDeleteConcurrentNodeChild(ConcurrentNode<T> node)
        {
            foreach (ConcurrentNode<T> next in node.Child)
            {
                RecursiveDeleteConcurrentNodeChild(next);
                cacheLock.EnterWriteLock();
                try
                {
                    next.Child = null;
                }
                finally
                {
                    cacheLock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Do not include root node itself.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public int CountLeaves(ConcurrentNode<T> node)
        {
            Queue<List<ConcurrentNode<T>>> q = new Queue<List<ConcurrentNode<T>>>();
            int leavesCount = 0;

            q.Enqueue(node.Child);
            while (0 < q.Count)
            {
                List<ConcurrentNode<T>> next = q.Dequeue();
                foreach (ConcurrentNode<T> leaf in next)
                {
                    leavesCount++;
                    if (0 < leaf.Child.Count)
                        q.Enqueue(leaf.Child);
                }
            }

            return leavesCount;
        }

        public ConcurrentNode<T> SearchNode(int id)
        {
            if (id == 0)
            {
                return null;
            }
            else
            { // Start from root
                List<ConcurrentNode<T>> unused;
                cacheLock.EnterReadLock();
                try
                {
                    return RecursiveSearchNode(id, root, out unused);
                }
                finally
                {
                    cacheLock.ExitReadLock();
                }
            }
        }

        public ConcurrentNode<T> SearchNode(int id, out List<ConcurrentNode<T>> sibling)
        {
            if (id == 0)
            {
                sibling = null;
                return null;
            }
            else
            { // Start from root
                cacheLock.EnterReadLock();
                try
                {
                    return RecursiveSearchNode(id, root, out sibling);
                }
                finally
                {
                    cacheLock.ExitReadLock();
                }
            }
        }

        private ConcurrentNode<T> RecursiveSearchNode(int id, List<ConcurrentNode<T>> list, out List<ConcurrentNode<T>> sibling)
        {
            foreach (ConcurrentNode<T> node in list)
            {
                if (id == node.Id)
                {
                    sibling = list;
                    return node;
                }
            }

            // Not found, start DFS
            foreach (ConcurrentNode<T> node in list)
            {
                return RecursiveSearchNode(node.Id, node.Child, out sibling);
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

        public ConcurrentNode<T> GetNext(int id)
        {
            List<ConcurrentNode<T>> sibling;
            ConcurrentNode<T> ConcurrentNode = SearchNode(id, out sibling);
            cacheLock.EnterReadLock();
            try
            {
                int idx = sibling.IndexOf(ConcurrentNode);
                if (idx + 1 < sibling.Count)
                    return sibling[idx + 1];
                else
                    return null;
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
        }

        public IEnumerator GetEnumerator()
        {
            return GetEnumeratorDFS();
        }

        public IEnumerator GetEnumeratorBFS()
        {
            Queue<List<ConcurrentNode<T>>> q = new Queue<List<ConcurrentNode<T>>>();
            Queue<ConcurrentNode<T>> qFinal = new Queue<ConcurrentNode<T>>();

            cacheLock.EnterReadLock();
            try
            {
                q.Enqueue(root);
                while (0 < q.Count)
                {
                    List<ConcurrentNode<T>> next = q.Dequeue();
                    foreach (ConcurrentNode<T> node in next)
                    {
                        qFinal.Enqueue(node);
                        if (0 < node.Child.Count)
                            q.Enqueue(node.Child);
                    }
                }
            }
            finally
            {
                cacheLock.ExitReadLock();
            }
            
            while (0 < qFinal.Count)
                yield return qFinal.Dequeue();
        }

        public IEnumerator GetEnumeratorDFS()
        { // Not tested
            Queue<ConcurrentNode<T>> qFinal = new Queue<ConcurrentNode<T>>();
            cacheLock.EnterReadLock();
            try
            {
                RecursiveGetEnumeratorDFS(root, qFinal);
            }
            finally
            {
                cacheLock.ExitReadLock();
            }

            while (0 < qFinal.Count)
                yield return qFinal.Dequeue();
        }

        private void RecursiveGetEnumeratorDFS(List<ConcurrentNode<T>> list, Queue<ConcurrentNode<T>> qFinal)
        {
            foreach (ConcurrentNode<T> node in list)
            {
                qFinal.Enqueue(node);
                if (0 < node.Child.Count)
                    RecursiveGetEnumeratorDFS(node.Child, qFinal);
            }
        }
    }

    public class ConcurrentNode<T>
    {
        public int Id;
        public int ParentId; // 0 is root ConcurrentNodeList
        public T Data;
        public List<ConcurrentNode<T>> Parent;
        public List<ConcurrentNode<T>> Child;

        public ConcurrentNode(int parentId, int id, T data, List<ConcurrentNode<T>> parent)
        {
            this.ParentId = parentId;
            this.Id = id;
            this.Data = data;
            this.Parent = parent;
            this.Child = new List<ConcurrentNode<T>>();
        }
    }
}
