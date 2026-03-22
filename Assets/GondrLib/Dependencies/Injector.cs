using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GondrLib.Dependencies
{
    [DefaultExecutionOrder(-10)] //0이 일반 스크립트
    public class Injector : MonoBehaviour
    {
        private const BindingFlags _BindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        
        private readonly Dictionary<Type, object> _registry = new Dictionary<Type, object>();
        //대문자 Object는 유니티 오브젝트이다.

        private void Awake()
        {
            //인터페이스를 구현한 모든 녀석을 가져와서 Provide 어트리뷰트가 있는 녀석을 찾아서 딕셔너리에 넣는다.
            IEnumerable<IDependencyProvider> providers = FindMonoBehaviours().OfType<IDependencyProvider>();
            foreach (IDependencyProvider pro in providers)
            {
                RegisterProvider(pro);
            }
            
            IEnumerable<MonoBehaviour> injectables = FindMonoBehaviours().Where(IsInjectable);
            foreach (var mono in injectables)
            {
                Inject(mono);
            }
        }

        private void Inject(MonoBehaviour mono)
        {
            Type type = mono.GetType();
            
            //해당 모노비해비어에서 Inject어트리뷰트가 선언된 Field만 다 가져온다.
            IEnumerable<FieldInfo> injectableFields = type.GetFields(_BindingFlags)
                .Where(f => Attribute.IsDefined(f, typeof(InjectAttribute)));

            foreach (var field in injectableFields)
            {
                Type fieldType = field.FieldType;
                object instance = ResolveType(fieldType); //해당 필드에 맞는 인스턴스를 가져온다.
                Debug.Assert(instance != null, $"Inject instance not found in registry : {fieldType.Name}");
                
                field.SetValue(mono, instance);
            }
            
            //해당 모노비해비어에서 Inject어트리뷰트가 선언된 Field만 다 가져온다.
            IEnumerable<MethodInfo> injectableMethods = type.GetMethods(_BindingFlags)
                .Where(f => Attribute.IsDefined(f, typeof(InjectAttribute)));

            foreach (var method in injectableMethods)
            {
                //파라메터의 타입정보만 가져온다.
                Type[] requireParam = method.GetParameters()
                    .Select(p => p.ParameterType).ToArray();
                //각 파라메터 타입을 리졸브해주면 넣어줄 인스턴스들의 리스트가 나오게 된다.
                object[] paramValues = requireParam.Select(ResolveType).ToArray();
                method.Invoke(mono, paramValues); //해당 인스턴스에 파라메터를 넣고 매서드 실행
            }
            
        }

        private object ResolveType(Type type)
        {
            _registry.TryGetValue(type, out object instance);
            return instance;
        }

        private bool IsInjectable(MonoBehaviour mono)
        {
            //멤버는 필드와 매서드를 모두 이야기한다.
            MemberInfo[] members = mono.GetType().GetMembers(_BindingFlags);
            return members.Any(member => Attribute.IsDefined(member, typeof(InjectAttribute)));
        }

        private void RegisterProvider(IDependencyProvider pro)
        {
            //클래스 그 자체에 Provide가 되는 경우 별도의 리플렉션 과정없이 해당 클래스를 바로 가져온다.
            if(Attribute.IsDefined(pro.GetType(), typeof(ProvideAttribute)))
            {
                _registry.Add(pro.GetType(), pro);
                return;
            }
            
            MethodInfo[] methods = pro.GetType().GetMethods(_BindingFlags);

            foreach (var method in methods)
            {
                if(!Attribute.IsDefined(method, typeof(ProvideAttribute))) continue;
                
                Type returnType = method.ReturnType; //매서드의 리턴타입을 알아내고
                object returnInstance = method.Invoke(pro, null); //해당 매서드를 실행해서 결과를 받고
                Debug.Assert(returnInstance != null, $"Provide method return void {method.Name}");
                
                _registry.Add(returnType, returnInstance);
            }
        }

        private IEnumerable<MonoBehaviour> FindMonoBehaviours()
        {
            return FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        }
    }
}