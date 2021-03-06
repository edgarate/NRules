﻿using System;
using System.Linq.Expressions;
using NRules.Rete;
using NRules.Utilities;

namespace NRules.AgendaFilters
{
    internal interface IActivationCondition
    {
        bool Invoke(AgendaContext context, Activation activation);
    }

    internal class ActivationCondition : IActivationCondition
    {
        private readonly LambdaExpression _expression;
        private readonly FastDelegate<Func<object[], bool>> _compiledExpression;
        private readonly IndexMap _tupleFactMap;

        public ActivationCondition(LambdaExpression expression, FastDelegate<Func<object[], bool>> compiledExpression, IndexMap tupleFactMap)
        {
            _expression = expression;
            _compiledExpression = compiledExpression;
            _tupleFactMap = tupleFactMap;
        }

        public bool Invoke(AgendaContext context, Activation activation)
        {
            var tuple = activation.Tuple;
            var activationFactMap = activation.FactMap;

            var args = new object[_compiledExpression.ArrayArgumentCount];

            int index = tuple.Count - 1;
            foreach (var fact in tuple.Facts)
            {
                var mappedIndex = _tupleFactMap[activationFactMap[index]];
                IndexMap.SetElementAt(args, mappedIndex, fact.Object);
                index--;
            }

            Exception exception = null;
            bool result = false;
            try
            {
                result = _compiledExpression.Delegate.Invoke(args);
                return result;
            }
            catch (Exception e)
            {
                exception = e;
                bool isHandled = false;
                context.EventAggregator.RaiseAgendaExpressionFailed(context.Session, e, _expression, args, activation, ref isHandled);
                throw new ExpressionEvaluationException(e, _expression, isHandled);
            }
            finally
            {
                context.EventAggregator.RaiseAgendaExpressionEvaluated(context.Session, exception, _expression, args, result, activation);
            }
        }
    }
}
