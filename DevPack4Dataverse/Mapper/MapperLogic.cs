/*
Copyright 2022 Kamil Skoracki / C485@GitHub

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace DevPack4Dataverse.Mapper;


//TODO
//public class MapperLogic<T> where T : class, new()
//{
//    private readonly SdkProxy _sdkProxy;
//    private MapperLogic<T> _nextStage, _previousStage;
//    private T obj;
//    private bool _hasFreshObject;

//    public MapperLogic(SdkProxy sdkProxy)
//    {
//        obj = new T();
//        _hasFreshObject = true;
//        _sdkProxy = sdkProxy;
//    }

//    public MapperLogic(T mapperObj, SdkProxy sdkProxy)
//    {
//        obj = mapperObj;
//    }

//    public MapperLogic(T mapperObj, MapperLogic<T> previousStage, SdkProxy sdkProxy)
//    {
//        obj = mapperObj;
//        _previousStage = previousStage;
//    }

//    public T Execute()
//    {
//        MapperLogic<T> topStage = this;
//        while(topStage._previousStage is not null)
//        {
//            topStage = topStage._previousStage;
//        }
//        return obj;
//    }

//    public MapperLogic<T> GetNextStage()
//    {
//        _nextStage = new MapperLogic<T>(obj, this, _sdkProxy);
//        return _nextStage;
//    }

//    public MapperLogic<T> MapSimpleType<TValue>(Expression<Func<T, TValue>> memberLamda, TValue value)
//    {
//        if (memberLamda.Body is not MemberExpression memberSelectorExpression)
//        {
//            return this;
//        }
//        PropertyInfo property = memberSelectorExpression.Member as PropertyInfo;
//        property?.SetValue(obj, value, null);
//        return this;
//    }

//    public MapperLogic<T> MapSimpleTypeFromMappedObject<TValue>(Expression<Func<T, TValue>> memberLamda, Expression<Func<T, TValue>> memberLamdaMappedObject)
//    {
//        Guard.Against.InvalidInput(_hasFreshObject, nameof(_hasFreshObject), p => p == true, "Mapping from mapped object is not possible at first stage.");
//        /*  if ((expression.Member as PropertyInfo) != null)
//    {
//        var exp = (MemberExpression) expression.Expression;
//        var constant = (ConstantExpression) exp.Expression;
//        var fieldInfoValue = ((FieldInfo) exp.Member).GetValue(constant.Value);
//        value = ((PropertyInfo) expression.Member).GetValue(fieldInfoValue, null);
//    }
//    else if ((expression.Member as FieldInfo) != null)
//    {
//        var fieldInfo = expression.Member as FieldInfo;
//        var constantExpression = expression.Expression as ConstantExpression;
//        if (fieldInfo != null & constantExpression != null)
//        {
//            value = fieldInfo.GetValue(constantExpression.Value);
//        }
//    }
//    else
//    {
//        throw new InvalidMemberException();
//    }*/
//        if (memberLamda.Body is not MemberExpression memberSelectorExpression)
//        {
//            return this;
//        }
//        if (memberLamdaMappedObject.Body is not MemberExpression memberSelectorExpressionMappedObject)
//        {
//            return this;
//        }
//        PropertyInfo property = memberSelectorExpression.Member as PropertyInfo;

//        TValue valueToSet = memberLamdaMappedObject.Compile().Invoke(obj);
//        property?.SetValue(obj, valueToSet, null);

//        return this;
//    }
//}

//public class test
//{
//    public test()
//    {
//        int incommingValue = 9;
//        var kk = new MapperLogic<xxx>();
//        xxx mappedObject = kk
//            .MapSimpleType(p => p.MyProperty, incommingValue)
//            .MapSimpleType(p => p.MyProperty2, 6)
//            .GetNextStage()
//            .MapSimpleTypeFromMappedObject(p => p.MyProperty, u => u.MyProperty2)
//            .MapSimpleTypeFromMappedObject(p => p.MyProperty2, u => u.XX)
//            .Execute();
//    }

//    public class xxx
//    {
//        public int MyProperty { get; set; }
//        public int MyProperty2 { get; set; }
//        public int XX;
//    }
//}
