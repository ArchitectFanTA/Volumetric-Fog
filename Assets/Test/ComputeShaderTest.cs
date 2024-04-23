using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComputeShaderTest : MonoBehaviour
{
    public ComputeShader test;
    public RenderTexture renderTexture;


    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if(renderTexture == null) 
        {
            renderTexture = new RenderTexture(256, 256,4);
            renderTexture.enableRandomWrite=true;
            renderTexture.Create();
            
        }
            
        test.SetTexture(0,"Result",renderTexture);
        test.Dispatch(0,renderTexture.width/8,renderTexture.height /8,1);
        Graphics.Blit(renderTexture, dest);
    }
        
    void Start()
    {
        renderTexture = new RenderTexture(256, 256, 4);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        
        test.SetTexture(0,"Result",renderTexture);
        test.Dispatch(0,renderTexture.width/8,renderTexture.height/8,1);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
