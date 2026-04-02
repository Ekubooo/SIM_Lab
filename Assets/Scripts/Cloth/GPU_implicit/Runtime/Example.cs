using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Example : MonoBehaviour
{
    private ClothSimulation _simulate = new ClothSimulation();
    [SerializeField]
    private ClothSimulation.SimulateSetting _setting = new ClothSimulation.SimulateSetting();
    
    // private Cloth_PBD _pbd = new Cloth_PBD();
    // [SerializeField]
    // private Cloth_PBD.SimulateSetting _pbd_setting = new Cloth_PBD.SimulateSetting();

    public GameObject ball;

    void Awake()
    {
        _simulate.UpdateSimulateSetting(_setting);
        this.UpdateBall();
        StartCoroutine(_simulate.StartAsync());
    }

    [ContextMenu("UpdateSetting")]
    private void UpdateSetting(){
        _simulate.UpdateSimulateSetting(_setting);
    }

    void UpdateBall(){
        var ballParams = (Vector4)ball.transform.position;
        ballParams.w = ball.transform.localScale.x / 2;
        _simulate.UpdateBallParams(ballParams);
    }

    void Update(){
        this.UpdateBall();
    }


    void OnDestroy(){
        _simulate.Dispose();
    }

    void OnRenderObject(){
        _simulate.Draw();
    }
}
