// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;

namespace NuGetGallery.OData.QueryInterceptors
{
    public class ODataRemoveSorter : ExpressionVisitor
    {
        private const string ThenByOperator = "ThenBy";
        private const string ThenByDescendingOperator = "ThenByDescending";

        private string _columnName = string.Empty;

        public ODataRemoveSorter(string columnName) : base()
        {
            _columnName = columnName;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsSortingOnColumnName(node))
            {
                // The expression is of the format Queryable.ThenBy(OrderBy(<Expression>, <Order-by-params>), <Then-by-params>). To avoid performing the 
                // method, we ignore it, traversing the passed in expression instead.
                return Visit(node.Arguments[0]);
            }
            return base.VisitMethodCall(node);
        }

        private bool IsSortingOnColumnName(MethodCallExpression expression)
        {
            var methodsToIgnore = new[] { ThenByOperator, ThenByDescendingOperator };
            var method = expression.Method;

            if (method.DeclaringType == typeof(Queryable) && methodsToIgnore.Contains(method.Name, StringComparer.Ordinal))
            {
                return IsColumnNameArgument(expression);
            }

            return false;
        }

        private bool IsColumnNameArgument(MethodCallExpression expression)
        {
            if (expression.Arguments.Count == 2)
            {
                var memberVisitor = new MemberVisitor(_columnName);
                memberVisitor.Visit(expression.Arguments[1]);
                return memberVisitor.Flag;
            }

            return false;
        }

        private sealed class MemberVisitor : ExpressionVisitor
        {
            private string _columnName = string.Empty;

            public MemberVisitor(string columnName) : base()
            {
                _columnName = columnName;
            }
            public bool Flag { get; private set; }

            protected override Expression VisitMember(MemberExpression node)
            {
                // Note that if Flag has already been set to true, we need to retain that state
                // as our visitor can be called multiple times.
                // Example using Version column: The expression can either be p => p.Version or p => p.ExpandedWrapper.Version where the 
                // latter is some funky OData type wrapper. We need to ensure we handle both these cases
                Flag = Flag || String.Equals(node.Member.Name, _columnName, StringComparison.Ordinal);
                return base.VisitMember(node);
            }
        }
    }
}