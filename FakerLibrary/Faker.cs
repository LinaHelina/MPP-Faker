﻿using FakerLibrary.Generators.TypesGenerators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FakerLibrary
{
    public class Faker
    {
        private List<IGenerator> gens = new List<IGenerator>
        {
           new ListGenerator(),
           new BoolGenerator(),
           new DataGenerator(),
           new DoubleGenerator(),
           new FloatGenerator(),
           new LongGenerator(),
           new ShortGenerator(),
           new StringGenerator()
        };

        public int MaxCircularDependency { get; set; } = 0;
        public int currentCircularDependency = 0;
        public Stack<Type> constructionStack = new Stack<Type>();
        private Random random;


        public Faker()
        {
            random = new Random();
            LoadGenerators();            
          
        }

        public T Create<T>() 
        {
            return (T)Create(typeof(T));
        }

        internal object Create(Type type) 
        {
            if(((currentCircularDependency = constructionStack.Where(t => t.Equals(type)).Count()) > MaxCircularDependency))
            {
                return GetDefaultValue(type);
            }
            constructionStack.Push(type);

            IGenerator currentGenerator = null;
            foreach(IGenerator g in gens)
            {
                if(g.CanGenerate(type))
                {
                    currentGenerator = g;
                    break;
                }
            }

            if (currentGenerator!=null)
            {
                constructionStack.Pop();
                return currentGenerator.Generate(new GeneratorContext(random,type,this));
            }

            object createdObject = CreateObject(type);

            if(createdObject == null)
            {
                constructionStack.Pop();
                return GetDefaultValue(type);

            }

            constructionStack.Pop();
            return createdObject;
        }

        private static object GetDefaultValue(Type t)
        {
            if (t.IsValueType)
                return Activator.CreateInstance(t);
            else
                return null;
        }

        private object CreateObject(Type type)
        {
            ConstructorInfo[] currentConstructors = type.GetConstructors();
            object createdObject = default;

            if (currentConstructors.Length == 0 && type.IsClass)
                return default;

            ParameterInfo[] ctorParamInfos = null;
            ConstructorInfo chosenConstructor = null;
            bool isCreated = true;
            foreach (ConstructorInfo cInfo in currentConstructors.OrderByDescending(c => c.GetParameters().Length))
            {
                ParameterInfo[] parametersInfo = cInfo.GetParameters();
                object[] parameters = new object[parametersInfo.Length];
                for (int i = 0; i < parameters.Length; i++)
                    parameters[i] = Create(parametersInfo[i].ParameterType);

                try
                {
                    createdObject = cInfo.Invoke(parameters);
                    ctorParamInfos = parametersInfo;
                }
                catch
                {
                    isCreated = false;
                    continue;
                }
                if (isCreated)
                    break;
            }

            if(createdObject == null && type.IsValueType)
            {
                try
                {
                    return Activator.CreateInstance(type);
                }

                catch
                {
                    return null;
                }

            }
            else if (createdObject == null)
            {
                return null;
            }


            GenerateFieldsAndProperties(createdObject, ctorParamInfos, chosenConstructor);
            return createdObject;
        }

        private void GenerateFieldsAndProperties(object createdObject, ParameterInfo[] ctorParams, ConstructorInfo cInfo)
        {
            var fields = createdObject.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).Cast<MemberInfo>();
            var properties = createdObject.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Cast<MemberInfo>();
            var fieldsAndProperties = fields.Concat(properties);

            foreach (MemberInfo m in fieldsAndProperties)
            {

                bool wasInitialized = false;

                Type memberType = (m as FieldInfo)?.FieldType ?? (m as PropertyInfo)?.PropertyType;
                object memberValue = (m as FieldInfo)?.GetValue(createdObject) ?? (m as PropertyInfo)?.GetValue(createdObject);

                for (int i = 0; i < ctorParams?.Length; i++)
                {
                    object defaultValue = GetDefaultValue(memberType);
                    if ((ctorParams != null && ctorParams[i] == memberValue && memberType == ctorParams[i].ParameterType && m.Name == ctorParams[i].Name) || defaultValue?.Equals(memberValue) == false)
                    {
                        wasInitialized = true;
                        break;
                    }
                }
                if (!wasInitialized)
                {

                    (m as FieldInfo)?.SetValue(createdObject, Create(((FieldInfo)m).FieldType));
                    if ((m as PropertyInfo)?.CanWrite == true)
                        ((PropertyInfo)m).SetValue(createdObject, Create(((PropertyInfo)m).PropertyType));
                }
            }

        }

        private static bool IsDefaultValue(object obj, MemberInfo mi)
        {
            if ((mi as FieldInfo)?.GetValue(obj) == GetDefaultValue((mi as FieldInfo).FieldType))
                return true;
            else if ((mi as PropertyInfo)?.GetValue(obj) == GetDefaultValue((mi as PropertyInfo).PropertyType))
                return true;
            return false;
        }


        private void LoadGenerators()
        {

            string pluginsPath = @"d:\Ангелина\5 сем\5 сем\СПП\Lab2-MPP\MPP-Faker\pl";
            //string pluginsPath = Directory.GetCurrentDirectory() + @"\Plugins";
            string[] f = Directory.GetFiles(pluginsPath, "*.dll");
             foreach ( string name in Directory.GetFiles(pluginsPath, "*.dll"))
             {
                 Assembly asm = Assembly.LoadFrom(name);
                 foreach (Type t in asm.GetTypes())
                 {

                     if (IsRequiredType(t, typeof(Generator<>)))
                     {
                         var currentGenerator = Activator.CreateInstance(t);
                         gens.Add((IGenerator)currentGenerator);
                     }
                 }
            } 
        }

        private bool IsRequiredType(Type current, Type isRequired)
        {
            while(current!=null && current!=typeof(object))
            {
                Type currType = current.IsGenericType ? current.GetGenericTypeDefinition() : current;
                if (isRequired == currType)
                    return true;
                current = current.BaseType;
            }
            return false;
        }

    }
}
