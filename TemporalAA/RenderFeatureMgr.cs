/****************************************************
    文件：RenderFeatureMgr.cs
    作者：#CREATEAUTHOR#
    邮箱:  gaocanjun@baidu.com
    日期：#CREATETIME#
    功能：Todo
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using System.Linq;
using System.Reflection;

public class RenderFeatureMgr : MonoBehaviour
{
    public ScriptableRendererData RendererData;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        //if (Input.GetKeyUp(KeyCode.A))
        //{
        //    ToggleRenderFeature();
        //}
    }

    public void ToggleRenderFeature()
    {
        // 禁用 Render Feature
        RendererData.rendererFeatures[1].SetActive(false);

    }

    private UniversalRendererData GetRendererData()
    {
        // 通过反射获取渲染数据, 也可以通过Resources.Load加载, 但是需要将UniversalRendererData文件放在Resources目录下
        UniversalRenderPipelineAsset urpAsset = GraphicsSettings.renderPipelineAsset as UniversalRenderPipelineAsset;
        FieldInfo propertyInfo = urpAsset.GetType().GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
        ScriptableRendererData[] rendererDatas = (ScriptableRendererData[])propertyInfo.GetValue(urpAsset);
        if (rendererDatas != null && rendererDatas.Length > 0 && (rendererDatas[0] is UniversalRendererData))
        {
            return rendererDatas[0] as UniversalRendererData;
        }
        return null;
    }

    //private FullscreenFeature GetFeature(string name)
    //{ // 获取feature
    //    UniversalRendererData rendererData = Resources.Load<UniversalRendererData>("Full Universal Renderer Data");
    //    if (rendererData != null && !string.IsNullOrEmpty(name))
    //    {
    //        List<FullscreenFeature> features = rendererData.rendererFeatures.OfType<FullscreenFeature>().ToList();
    //        foreach (FullscreenFeature feature in features)
    //        {
    //            if (name.Equals(feature.name))
    //            {
    //                return feature;
    //            }
    //        }
    //    }
    //    return null;
    //}
}
