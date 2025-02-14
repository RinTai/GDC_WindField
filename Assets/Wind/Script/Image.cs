using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class Image : MonoBehaviour
{
    public WindManager manager;
    public WindRender render;
    public RawImage rawImage;
    private void Update()
    {
        if(manager.windTexture != null)
            rawImage.texture = manager.windTexture;
        else
            rawImage.texture = WindRender.m_WindFieldPass.windTexture;
        
    }
}
