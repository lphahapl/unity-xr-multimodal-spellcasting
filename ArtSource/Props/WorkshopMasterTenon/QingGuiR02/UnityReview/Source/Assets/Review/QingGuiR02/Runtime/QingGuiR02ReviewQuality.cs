using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace NarutoVR.QingGuiR02Review {
public sealed class QingGuiR02ReviewQuality:MonoBehaviour {
 public UniversalRenderPipelineAsset reviewPipeline;
 RenderPipelineAsset previous;
 void Awake(){previous=QualitySettings.renderPipeline;if(reviewPipeline)QualitySettings.renderPipeline=reviewPipeline;}
 void OnDestroy(){if(QualitySettings.renderPipeline==reviewPipeline)QualitySettings.renderPipeline=previous;}
}}
