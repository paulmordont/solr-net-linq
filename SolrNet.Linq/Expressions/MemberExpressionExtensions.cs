﻿using SolrNet.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;
using SolrNet.Impl;
using SolrNet.Linq.Expressions.Context;

namespace SolrNet.Linq.Expressions
{
    public static class MemberExpressionExtensions
    {
        private static readonly Dictionary<ExpressionType, Func<BinaryExpression, MemberContext, string>> BinaryHelper = new Dictionary<ExpressionType, Func<BinaryExpression, MemberContext, string>> {
            { ExpressionType.Divide ,(b,t) => $"div({b.Left.GetSolrMemberProduct(t)},{b.Right.GetSolrMemberProduct(t)})" },
            { ExpressionType.Subtract ,(b,t) => $"sub({b.Left.GetSolrMemberProduct(t)},{b.Right.GetSolrMemberProduct(t)})" },
            { ExpressionType.Multiply ,(b,t) => $"mul({b.Left.GetSolrMemberProduct(t)},{b.Right.GetSolrMemberProduct(t)})" },
            { ExpressionType.Add ,(b,t) => $"sum({b.Left.GetSolrMemberProduct(t)},{b.Right.GetSolrMemberProduct(t)})" },
        };

        private static readonly Dictionary<string, Func<MethodCallExpression, MemberContext, string>> CallHelper = new Dictionary<string, Func<MethodCallExpression, MemberContext, string>> {
            { typeof(Math).FullName + nameof(Math.Abs) ,(c,t) => $"abs({c.Arguments[0].GetSolrMemberProduct(t)})" },
            { typeof(Math).FullName + nameof(Math.Log10) ,(c,t) => $"log({c.Arguments[0].GetSolrMemberProduct(t)})" },
            { typeof(Math).FullName + nameof(Math.Max) ,(c,t) => $"max({c.Arguments[0].GetSolrMemberProduct(t)},{c.Arguments[1].GetSolrMemberProduct(t)})" },
            { typeof(Math).FullName + nameof(Math.Min) ,(c,t) => $"min({c.Arguments[0].GetSolrMemberProduct(t)},{c.Arguments[1].GetSolrMemberProduct(t)})" },
            { typeof(Math).FullName + nameof(Math.Pow) ,(c,t) => $"pow({c.Arguments[0].GetSolrMemberProduct(t)},{c.Arguments[1].GetSolrMemberProduct(t)})" },
            { typeof(Math).FullName + nameof(Math.Sqrt) ,(c,t) => $"sqrt({c.Arguments[0].GetSolrMemberProduct(t)})" },
        };

        private static readonly Dictionary<string, Func<MethodCallExpression, MemberContext, string>> TransformersHelper = new Dictionary<string, Func<MethodCallExpression, MemberContext, string>> {
            { nameof(SolrExpr.Transformers.Value) + typeof(string).Name ,(c,t) => $"[value v='{c.Arguments[0].GetSolrMemberProduct(t)}']" },
            { nameof(SolrExpr.Transformers.Value) + typeof(int).Name ,(c,t) => $"[value v={c.Arguments[0].GetSolrMemberProduct(t)} t=int]" },
            { nameof(SolrExpr.Transformers.Value) + typeof(float).Name ,(c,t) => $"[value v={c.Arguments[0].GetSolrMemberProduct(t)} t=float]" },
            { nameof(SolrExpr.Transformers.Value) + typeof(double).Name ,(c,t) => $"[value v={c.Arguments[0].GetSolrMemberProduct(t)} t=double]" },
            { nameof(SolrExpr.Transformers.Value) + typeof(DateTime).Name ,(c,t) => $"[value v={c.Arguments[0].GetSolrMemberProduct(t)} t=date]" },

            { nameof(SolrExpr.Transformers.ExplainHtml) + typeof(string).Name ,(c,t) => "[explain style=html]" },
            //{ nameof(SolrExpr.Transformers.ExplainNl) + typeof(XElement).Name ,(c,t) => "[explain style=nl]" },
            { nameof(SolrExpr.Transformers.ExplainText) + typeof(string).Name ,(c,t) => "[explain style=text]" },

            { nameof(SolrExpr.Transformers.DocId) + typeof(int).Name ,(c,t) => "[docid]" },

        };
        private static readonly Dictionary<string, Func<MethodCallExpression, MemberContext, string>> FieldsHelper = new Dictionary<string, Func<MethodCallExpression, MemberContext, string>> {
            { nameof(SolrExpr.Fields.Score) ,(c,t) => "score" },

        };

        internal static string GetSolrMemberProduct(this Expression exp, MemberContext context, bool disableFunctions = false)
        {
            try
            {
                exp = exp.HandleConversion();

                if (exp is MemberExpression me)
                {
                    MemberInfo memberInfo = me.Member;
                    
                    if (context.IsAccessToMember(me))
                    {
                        return context.GetMemberSolrName(me.Member);
                    }

                    if (me.Member.DeclaringType != null &&
                        me.Member.DeclaringType.IsGenericType &&
                        me.Member.DeclaringType.Name.StartsWith(nameof(Nullable)) &&
                        me.Member.Name == nameof(Nullable<int>.Value)) // int may be replaced to any other type
                    {
                        return me.Expression.GetSolrMemberProduct(context);
                    }
                }

                if (!disableFunctions && exp is BinaryExpression bin)
                {
                    if (BinaryHelper.ContainsKey(bin.NodeType))
                    {
                        return BinaryHelper[bin.NodeType].Invoke(bin, context);
                    }
                }

                if (exp is MethodCallExpression call)
                {
                    if (!disableFunctions)
                    {
                        string key = call.Method.DeclaringType.FullName + call.Method.Name;
                        if (CallHelper.ContainsKey(key))
                        {
                            return CallHelper[key].Invoke(call, context);
                        }

                        if (call.Method.DeclaringType == typeof(SolrExpr.Transformers))
                        {
                            string tkey = call.Method.Name + call.Method.ReturnType.Name;
                            if (TransformersHelper.ContainsKey(tkey))
                            {
                                return TransformersHelper[tkey].Invoke(call, context);
                            }
                        }

                        if (call.Method.DeclaringType == typeof(SolrExpr.Fields))
                        {
                            if (FieldsHelper.ContainsKey(call.Method.Name))
                            {
                                return FieldsHelper[call.Method.Name].Invoke(call, context);
                            }
                        }
                    }                    
                }

                // Access to member of other type can't be translated, so assume it should be used as a value
                object value = Expression.Lambda(exp).Compile().DynamicInvoke();
                
                return value.SerializeToSolr(context.FieldSerializer);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException($"Unable to translate SOLR expression {exp}", exception);
            }            
        }

        internal static string SerializeToSolr(this object value, ISolrFieldSerializer serializer)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            if (value == null)
            {
                return null;
            }

            if (serializer.CanHandleType(value.GetType()))
            {
                return serializer.Serialize(value).First().FieldValue;
            }

            throw new InvalidOperationException($"Unable to serialize '{value}'.");
        }
    }
}
