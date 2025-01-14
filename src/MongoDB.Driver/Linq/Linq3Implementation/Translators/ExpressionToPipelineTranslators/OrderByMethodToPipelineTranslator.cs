﻿/* Copyright 2010-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Linq;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Linq.Linq3Implementation.Ast;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Stages;
using MongoDB.Driver.Linq.Linq3Implementation.Misc;
using MongoDB.Driver.Linq.Linq3Implementation.Reflection;
using MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToAggregationExpressionTranslators;

namespace MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToPipelineTranslators
{
    internal static class OrderByMethodToPipelineTranslator
    {
        // public static methods
        public static AstPipeline Translate(TranslationContext context, MethodCallExpression expression)
        {
            var method = expression.Method;
            var arguments = expression.Arguments;

            if (method.IsOneOf(QueryableMethod.OrderBy, QueryableMethod.OrderByDescending, QueryableMethod.ThenBy, QueryableMethod.ThenByDescending))
            {
                var sourceExpression = arguments[0];
                var pipeline = ExpressionToPipelineTranslator.Translate(context, sourceExpression);

                var keySelectorLambda = ExpressionHelper.UnquoteLambda(arguments[1]);
                var sortField = CreateSortField(context, method.Name, keySelectorLambda, parameterSerializer: pipeline.OutputSerializer);
                switch (method.Name)
                {
                    case "OrderBy":
                    case "OrderByDescending":
                        pipeline = pipeline.AddStages(pipeline.OutputSerializer, AstStage.Sort(sortField));
                        break;

                    case "ThenBy":
                    case "ThenByDescending":
                        var oldSortStage = (AstSortStage)pipeline.Stages.Last();
                        var newSortStage = oldSortStage.AddSortField(sortField);
                        pipeline = pipeline.ReplaceLastStage(pipeline.OutputSerializer, newSortStage);
                        break;
                }

                return pipeline;
            }

            throw new ExpressionNotSupportedException(expression);
        }

        // private static methods
        private static AstSortField CreateSortField(TranslationContext context, string methodName, LambdaExpression keySelector, IBsonSerializer parameterSerializer)
        {
            var fieldPath = keySelector.GetFieldPath(context, parameterSerializer);
            switch (methodName)
            {
                case "OrderBy":
                case "ThenBy":
                    return AstSort.Field(fieldPath, AstSortOrder.Ascending);
                case "OrderByDescending":
                case "ThenByDescending":
                    return AstSort.Field(fieldPath, AstSortOrder.Descending);
                default:
                    throw new ArgumentException("Unexpected method name.", nameof(methodName));
            }
        }
    }
}
