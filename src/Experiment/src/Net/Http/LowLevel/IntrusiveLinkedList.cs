// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Net.Http.LowLevel
{
    internal struct IntrusiveLinkedList<TNode> where TNode : class, IIntrusiveLinkedListNode<TNode>
    {
        private TNode? _first, _last;

        public TNode? Front => _first;
        public TNode? Back => _last;

        public void PushFront(TNode node)
        {
            ref IntrusiveLinkedNodeHeader<TNode> header = ref node.ListHeader;

            Debug.Assert(header.Prev == null);
            Debug.Assert(header.Next == null);

            if (_first is TNode first)
            {
                header.Next = first;
                first.ListHeader.Prev = node;
                _first = node;
            }
            else
            {
                _first = node;
                _last = node;
            }
        }

        public void PushBack(TNode node)
        {
            ref IntrusiveLinkedNodeHeader<TNode> header = ref node.ListHeader;

            Debug.Assert(header.Prev == null);
            Debug.Assert(header.Next == null);

            if (_last is TNode last)
            {
                header.Prev = last;
                last.ListHeader.Next = node;
                _last = node;
            }
            else
            {
                _first = node;
                _last = node;
            }
        }

        public TNode? PopFront()
        {
            if (_first is not TNode first)
            {
                return null;
            }

            ref IntrusiveLinkedNodeHeader<TNode> header = ref first.ListHeader;

            if (header.Next is TNode next)
            {
                next.ListHeader.Prev = null;
                header.Next = null;
                _first = next;
            }
            else
            {
                _first = null;
                _last = null;
            }

            return first;
        }

        public TNode? PopBack()
        {
            if (_last is not TNode last)
            {
                return null;
            }

            ref IntrusiveLinkedNodeHeader<TNode> header = ref last.ListHeader;

            if (header.Prev is TNode prev)
            {
                prev.ListHeader.Next = null;
                header.Prev = null;
                _last = prev;
            }
            else
            {
                _first = null;
                _last = null;
            }

            return last;
        }

        public void Remove(TNode node)
        {
            ref IntrusiveLinkedNodeHeader<TNode> header = ref node.ListHeader;

            TNode? prev = header.Prev;
            TNode? next = header.Next;

            if (prev is not null)
            {
                prev.ListHeader.Next = next;
                header.Prev = null;
            }
            else
            {
                Debug.Assert(node == _first);
                _first = next;
            }

            if (next is not null)
            {
                next.ListHeader.Prev = prev;
                header.Next = null;
            }
            else
            {
                Debug.Assert(node == _last);
                _last = prev;
            }
        }
    }

    internal struct IntrusiveLinkedNodeHeader<TNode> where TNode : class, IIntrusiveLinkedListNode<TNode>
    {
        public TNode? Prev, Next;
    }

    internal interface IIntrusiveLinkedListNode<TNode> where TNode : class, IIntrusiveLinkedListNode<TNode>
    {
        public ref IntrusiveLinkedNodeHeader<TNode> ListHeader { get; }
    }
}
