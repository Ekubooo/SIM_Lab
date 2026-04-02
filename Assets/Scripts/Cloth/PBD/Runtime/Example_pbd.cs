using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Example_pbd : MonoBehaviour
{
    private Cloth_PBD _simulate = new Cloth_PBD();
    [SerializeField]
    private Cloth_PBD.SimulateSetting _setting = new Cloth_PBD.SimulateSetting();

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
