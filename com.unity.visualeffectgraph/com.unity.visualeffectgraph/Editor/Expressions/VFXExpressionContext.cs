using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using UnityEngine.Profiling;
using UnityEngine.VFX;

namespace UnityEditor.VFX
{
    [Flags]
    enum VFXExpressionContextOption
    {
        None = 0,
        Reduction = 1 << 0,
        CPUEvaluation = 1 << 1,
        ConstantFolding = 1 << 2,
        GPUDataTransformation = 1 << 3,
        PatchReadToEventAttribute = 1 << 4
    }

    abstract partial class VFXExpression
    {
        public class Context
        {
            private bool Has(VFXExpressionContextOption options)
            {
                return (m_ReductionOptions & options) == options;
            }

            private bool HasAny(VFXExpressionContextOption options)
            {
                return (m_ReductionOptions & options) != 0;
            }

            public Context(VFXExpressionContextOption reductionOption, List<VFXLayoutElementDesc> globalEventAttibutes = null)
            {
                m_ReductionOptions = reductionOption;
                m_GlobalEventAttribute = globalEventAttibutes;

                if (Has(VFXExpressionContextOption.CPUEvaluation) && Has(VFXExpressionContextOption.GPUDataTransformation))
                    throw new ArgumentException("Invalid reduction options");
            }

            public void RegisterExpression(VFXExpression expression, VFXContext sourceContext = null)
            {
                if (!m_EndExpressions.TryGetValue(expression, out var contexts))
                {
                    contexts = new();
                    m_EndExpressions.Add(expression, contexts);
                }

                if (sourceContext != null)
                {
                    if (!contexts.Add(sourceContext))
                        throw new InvalidOperationException("Trying to add twice the same context for the same expression.");
                }
            }

            public void UnregisterExpression(VFXExpression expression)
            {
                Invalidate(expression);
                m_EndExpressions.Remove(expression);
            }

            public void Compile()
            {
                Profiler.BeginSample("VFXEditor.CompileExpressionContext");

                try
                {
                    bool needToPatch = HasAny(VFXExpressionContextOption.GPUDataTransformation | VFXExpressionContextOption.PatchReadToEventAttribute);
                    var gpuTransformation = Has(VFXExpressionContextOption.GPUDataTransformation);
                    var spawnEventPath = Has(VFXExpressionContextOption.PatchReadToEventAttribute);
                    
                    var collectedData = new CompileCollectedData()
                    {
                        bufferTypeUsages = new(),
                        hlslCodeHolders = new()
                    };

                    foreach (var exp in m_EndExpressions)
                    {
                        Compile(exp.Key, collectedData);
                        if (needToPatch)
                        {
                            m_ReducedCache[exp.Key] = PatchVFXExpression(GetReduced(exp.Key), 
                                null /* no source in end expression */, 
                                gpuTransformation, 
                                spawnEventPath, 
                                m_GlobalEventAttribute, collectedData);
                        }

                        if (collectedData.bufferTypeUsages.Count > 0)
                        {
                            foreach (var context in exp.Value)
                            {
                                if (!m_GraphicsBufferTypeUsagePerContext.TryGetValue(context, out var usages))
                                {
                                    usages = new Dictionary<VFXExpression, BufferUsage>();
                                    m_GraphicsBufferTypeUsagePerContext.Add(context, usages);
                                }

                                foreach (var expressionTypeUsage in collectedData.bufferTypeUsages)
                                {
                                    if (!usages.TryAdd(expressionTypeUsage.Key, expressionTypeUsage.Value) && usages[expressionTypeUsage.Key] != expressionTypeUsage.Value)
                                    {
                                        throw new InvalidOperationException($"Diverging type usage for GraphicsBuffer : {usages[expressionTypeUsage.Key]}, {expressionTypeUsage.Value}");
                                    }
                                }
                            }
                        }
                        collectedData.bufferTypeUsages.Clear();

                        if (collectedData.hlslCodeHolders.Count > 0)
                        {
                            foreach (var context in exp.Value)
                            {
                                if (!m_HLSLCollectionPerContext.TryGetValue(context, out var codeHolders))
                                {
                                    codeHolders = new List<IHLSLCodeHolder>();
                                    m_HLSLCollectionPerContext.Add(context, codeHolders);
                                }
                                codeHolders.AddRange(collectedData.hlslCodeHolders);
                            }
                        }
                        collectedData.hlslCodeHolders.Clear();
                    }
                }
                finally
                {
                    Profiler.EndSample();
                }
            }

            public void Recompile()
            {
                Invalidate();
                Compile();
            }

            private bool ShouldEvaluate(VFXExpression exp, VFXExpression[] reducedParents)
            {
                if (!HasAny(VFXExpressionContextOption.Reduction | VFXExpressionContextOption.CPUEvaluation | VFXExpressionContextOption.ConstantFolding))
                    return false;

                if (exp.IsAny(Flags.NotCompilableOnCPU))
                    return false;

                if (!Has(VFXExpressionContextOption.CPUEvaluation) && exp.IsAny(Flags.InvalidConstant))
                    return false;

                if (!exp.Is(Flags.Value) && reducedParents.Length == 0) // not a value
                    return false;

                Flags flag = Flags.Value;
                if (!Has(VFXExpressionContextOption.CPUEvaluation))
                    flag |= Has(VFXExpressionContextOption.ConstantFolding) ? Flags.Foldable : Flags.Constant;

                if (exp.Is(Flags.Value) && ((exp.m_Flags & (flag | Flags.InvalidOnCPU)) != flag))
                    return false;

                return reducedParents.All(e => (e.m_Flags & (flag | Flags.InvalidOnCPU)) == flag);
            }

            private VFXExpression PatchVFXExpression(VFXExpression input, VFXExpression targetExpression, 
                bool insertGPUTransformation, bool patchReadAttributeForSpawn, 
                IEnumerable<VFXLayoutElementDesc> globalEventAttribute, CompileCollectedData collectedData)
            {
                if (insertGPUTransformation)
                {
                    switch (input.valueType)
                    {
                        case VFXValueType.ColorGradient:
                            input = new VFXExpressionBakeGradient(input);
                            break;
                        case VFXValueType.Curve:
                            input = new VFXExpressionBakeCurve(input);
                            break;

                        case VFXValueType.Mesh:
                        case VFXValueType.SkinnedMeshRenderer:
                            if (targetExpression != null)
                            {
                                if (input.valueType == VFXValueType.Mesh)
                                {
                                    switch (targetExpression.operation)
                                    {
                                        case VFXExpressionOperation.SampleMeshVertexFloat:
                                        case VFXExpressionOperation.SampleMeshVertexFloat2:
                                        case VFXExpressionOperation.SampleMeshVertexFloat3:
                                        case VFXExpressionOperation.SampleMeshVertexFloat4:
                                        case VFXExpressionOperation.SampleMeshVertexColor:
                                            var channelFormatAndDimensionAndStream = targetExpression.parents[2];
                                            channelFormatAndDimensionAndStream = Compile(channelFormatAndDimensionAndStream);
                                            if (!(channelFormatAndDimensionAndStream is VFXExpressionMeshChannelInfos))
                                                throw new InvalidOperationException("Unexpected type of expression in mesh sampling : " + channelFormatAndDimensionAndStream);
                                            input = new VFXExpressionVertexBufferFromMesh(input, channelFormatAndDimensionAndStream);
                                            break;
                                        case VFXExpressionOperation.SampleMeshIndex:
                                            input = new VFXExpressionIndexBufferFromMesh(input);
                                            break;
                                        default:
                                            throw new InvalidOperationException("Unexpected source operation for InsertGPUTransformation : " + targetExpression.operation);
                                    }
                                }
                                else //VFXValueType.SkinnedMeshRenderer
                                {
                                    if (targetExpression is IVFXExpressionSampleSkinnedMesh skinnedMeshExpression)
                                    {
                                        var channelFormatAndDimensionAndStream = targetExpression.parents[2];
                                        channelFormatAndDimensionAndStream = Compile(channelFormatAndDimensionAndStream);
                                        if (!(channelFormatAndDimensionAndStream is VFXExpressionMeshChannelInfos))
                                            throw new InvalidOperationException("Unexpected type of expression in skinned mesh sampling : " + channelFormatAndDimensionAndStream);
                                        input = new VFXExpressionVertexBufferFromSkinnedMeshRenderer(input, channelFormatAndDimensionAndStream, skinnedMeshExpression.frame);
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Unexpected source operation for InsertGPUTransformation : " + targetExpression);
                                    }
                                }
                            } //else sourceExpression is null, we can't determine usage but it's possible if value is declared but not used.
                            break;

                        /*case VFXValueType.Buffer:
                        {
                            //Save expression usage for later HLSL shader generation
                            if (targetExpression is VFXExpressionSampleBuffer)
                            {
                                var sampledType = (targetExpression as VFXExpressionSampleBuffer).GetSampledType();
                                if (!m_GraphicsBufferUsageType.TryGetValue(input, out var registeredType))
                                {
                                    m_GraphicsBufferUsageType.Add(input, sampledType);
                                }
                                else if (registeredType != sampledType)
                                {
                                    throw new InvalidOperationException(string.Format("Diverging type usage for GraphicsBuffer : {0}, {1}", registeredType, sampledType));
                                }
                            }
                        }
                        break;*/

                        default:
                            //Nothing to patch on this type
                            break;
                    }

                    /*if (input.valueType == VFXValueType.Buffer && targetExpression is VFXExpressionSampleBuffer)
                    {
                        if (!m_GraphicsBufferUsageType.ContainsKey(input))
                        {
                            m_GraphicsBufferUsageType.Add(input, (targetExpression as VFXExpressionSampleBuffer).GetSampledType());
                        }
                    }*/
                }

                if (input.valueType == VFXValueType.Buffer && input is VFXExpressionBufferWithType bufferWithType)
                {
                    input = input.parents[0]; //Explicitly skip NoOp expression
                    if (collectedData.bufferTypeUsages != null)
                    {
                        var usageType = bufferWithType.usage;
                        if (!collectedData.bufferTypeUsages.TryGetValue(input, out var registeredType))
                        {
                            collectedData.bufferTypeUsages.Add(input, usageType);
                        }
                        else if (registeredType != usageType)
                        {
                            throw new InvalidOperationException($"Diverging type usage for GraphicsBuffer : {registeredType}, {usageType}");
                        }
                    }
                }
                
                if (patchReadAttributeForSpawn && input is VFXAttributeExpression)
                {
                    var attribute = input as VFXAttributeExpression;
                    if (attribute.attributeLocation == VFXAttributeLocation.Current)
                    {
                        if (globalEventAttribute == null)
                            throw new InvalidOperationException("m_GlobalEventAttribute is null");

                        var layoutDesc = globalEventAttribute.FirstOrDefault(o => o.name == attribute.attributeName);
                        if (layoutDesc.name != attribute.attributeName)
                            throw new InvalidOperationException("Unable to find " + attribute.attributeName + " in globalEventAttribute");

                        input = new VFXReadEventAttributeExpression(attribute.attribute, layoutDesc.offset.element);
                    }
                }

                return input;
            }

            public struct CompileCollectedData
            {
                public Dictionary<VFXExpression, BufferUsage> bufferTypeUsages;
                public List<IHLSLCodeHolder> hlslCodeHolders;
            }
            
            public VFXExpression Compile(VFXExpression expression, CompileCollectedData collectedData = default(CompileCollectedData))
            {
                var gpuTransformation = Has(VFXExpressionContextOption.GPUDataTransformation);
                var patchReadAttributeForSpawn = Has(VFXExpressionContextOption.PatchReadToEventAttribute);

                VFXExpression reduced;
                if (!m_ReducedCache.TryGetValue(expression, out reduced))
                {
                    var parents = expression.parents.Select(e =>
                    {
                        var parent = Compile(e, collectedData);
                        bool currentGPUTransformation = gpuTransformation
                            && expression.IsAny(VFXExpression.Flags.NotCompilableOnCPU)
                            && !parent.IsAny(VFXExpression.Flags.NotCompilableOnCPU);
                        parent = PatchVFXExpression(parent, expression, currentGPUTransformation, patchReadAttributeForSpawn, m_GlobalEventAttribute, collectedData);
                        return parent;
                    }).ToArray();

                    if (ShouldEvaluate(expression, parents))
                    {
                        reduced = expression.Evaluate(parents);
                    }
                    else if (HasAny(VFXExpressionContextOption.Reduction | VFXExpressionContextOption.CPUEvaluation | VFXExpressionContextOption.ConstantFolding) || !parents.SequenceEqual(expression.parents))
                    {
                        reduced = expression.Reduce(parents);
                    }
                    else
                    {
                        reduced = expression;
                    }

                    if (expression is IHLSLCodeHolder hlslCodeHolder && collectedData.hlslCodeHolders != null)
                    {
                        if (!collectedData.hlslCodeHolders.Contains(hlslCodeHolder))
                            collectedData.hlslCodeHolders.Add(hlslCodeHolder);
                    }
                    
                    m_ReducedCache[expression] = reduced;
                }
                return reduced;
            }

            public void Invalidate()
            {
                m_ReducedCache.Clear();
                m_GraphicsBufferTypeUsagePerContext.Clear();
            }

            public void Invalidate(VFXExpression expression)
            {
                m_ReducedCache.Remove(expression);
            }

            public VFXExpression GetReduced(VFXExpression expression)
            {
                VFXExpression reduced;
                m_ReducedCache.TryGetValue(expression, out reduced);
                return reduced != null ? reduced : expression;
            }

            private void AddReducedGraph(HashSet<VFXExpression> dst, VFXExpression exp)
            {
                if (!dst.Contains(exp))
                {
                    dst.Add(exp);
                    foreach (var parent in exp.parents)
                        AddReducedGraph(dst, parent);
                }
            }

            public HashSet<VFXExpression> BuildAllReduced()
            {
                var reduced = new HashSet<VFXExpression>();
                foreach (var exp in m_EndExpressions)
                    if (m_ReducedCache.ContainsKey(exp.Key))
                        AddReducedGraph(reduced, m_ReducedCache[exp.Key]);
                return reduced;
            }

            public ReadOnlyCollection<VFXExpression> RegisteredExpressions { get { return m_EndExpressions.Keys.ToList().AsReadOnly(); } }

            // public IEnumerable<KeyValuePair<VFXExpression, Type>> GraphicsBufferUsageType { get { return m_GraphicsBufferUsageType; } }
            public Dictionary<VFXContext, Dictionary<VFXExpression, BufferUsage>> GraphicsBufferUsageType => m_GraphicsBufferTypeUsagePerContext;
            public Dictionary<VFXContext, List<IHLSLCodeHolder>> hlslCodeHoldersPerContext => m_HLSLCollectionPerContext;
            
            private Dictionary<VFXExpression, VFXExpression> m_ReducedCache = new Dictionary<VFXExpression, VFXExpression>();
            private Dictionary<VFXExpression, HashSet<VFXContext>> m_EndExpressions = new ();
            // private Dictionary<VFXExpression, Type> m_GraphicsBufferUsageType = new Dictionary<VFXExpression, Type>();
            private Dictionary<VFXContext, Dictionary<VFXExpression, BufferUsage>> m_GraphicsBufferTypeUsagePerContext = new ();
            
            private IEnumerable<VFXLayoutElementDesc> m_GlobalEventAttribute;
            private VFXExpressionContextOption m_ReductionOptions;
            private readonly Dictionary<VFXContext, List<IHLSLCodeHolder>> m_HLSLCollectionPerContext = new ();
        }
    }
}
