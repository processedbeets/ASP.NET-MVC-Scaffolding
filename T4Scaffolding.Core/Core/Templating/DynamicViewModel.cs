using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Reflection;

namespace T4Scaffolding.Core.Templating {
    [Serializable]
    public class DynamicViewModel : IDynamicMetaObjectProvider {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public object this[string name] {
            get {
                object result;
                return _values.TryGetValue(name, out result) ? result : null;
            }
            set {
                _values[name] = value;
            }
        }

        public DynamicMetaObject GetMetaObject(Expression parameter) {
            return new DynamicViewModelMetaObject(parameter, this);
        }

        private class DynamicViewModelMetaObject : DynamicMetaObject {
            private static readonly PropertyInfo ItemPropery = typeof(DynamicViewModel).GetProperty("Item");

            public DynamicViewModelMetaObject(Expression expression, object value)
                : base(expression, BindingRestrictions.Empty, value) {
            }

            private Expression GetDynamicExpression() {
                return Expression.Convert(Expression, typeof(DynamicViewModel));
            }

            private Expression GetIndexExpression(string key) {
                return Expression.MakeIndex(
                    GetDynamicExpression(),
                    ItemPropery,
                    new[] { Expression.Constant(key) }
                );
            }

            private Expression GetSetValueExpression(string key, DynamicMetaObject value) {
                return Expression.Assign(
                    GetIndexExpression(key),
                    Expression.Convert(value.Expression, typeof(object))
                );
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder) {
                var binderDefault = binder.FallbackGetMember(this);

                var expression = Expression.Convert(GetIndexExpression(binder.Name), typeof(object));

                var dynamicSuggestion = new DynamicMetaObject(expression, BindingRestrictions.GetTypeRestriction(Expression, LimitType)
                                                                                             .Merge(binderDefault.Restrictions));

                return binder.FallbackGetMember(this, dynamicSuggestion);
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) {
                var binderDefault = binder.FallbackSetMember(this, value);

                Expression expression = GetSetValueExpression(binder.Name, value);

                var dynamicSuggestion = new DynamicMetaObject(expression, BindingRestrictions.GetTypeRestriction(Expression, LimitType)
                                                                                             .Merge(binderDefault.Restrictions));

                return binder.FallbackSetMember(this, value, dynamicSuggestion);
            }

            public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
            {
                // VbGetBinder calls this method when you read a dynamic property from VB code
                var binderDefault = binder.FallbackInvokeMember(this, args);

                var expression = Expression.Convert(GetIndexExpression(binder.Name), typeof(object));

                var dynamicSuggestion = new DynamicMetaObject(expression, BindingRestrictions.GetTypeRestriction(Expression, LimitType)
                                                                                             .Merge(binderDefault.Restrictions));

                return binder.FallbackInvokeMember(this, args, dynamicSuggestion);
            }
        }

        #region Conversion from common PowerShell literals (hashtables, arrays)
        
        public static object FromObject(object value)
        {
            if (value == null)
                return null;
            if (value is PSObject)
                value = ((PSObject)value).BaseObject;
            if (value is string) // It's enumerable, but we don't want to enumerate it
                return value;
            if (value is Hashtable)
                return ConvertHashtable((Hashtable)value);
            if (value is IEnumerable)
                return ConvertEnumerable((IEnumerable)value);
            return value;
        }

        private static object ConvertEnumerable(IEnumerable enumerable)
        {
            return (from object item in enumerable
                    select FromObject(item)).ToList();
        }

        private static DynamicViewModel ConvertHashtable(Hashtable hashtable)
        {
            var result = new DynamicViewModel();
            foreach (DictionaryEntry entry in hashtable) {
                result[entry.Key.ToString()] = FromObject(entry.Value);
            }
            return result;
        }

        #endregion
    }

}