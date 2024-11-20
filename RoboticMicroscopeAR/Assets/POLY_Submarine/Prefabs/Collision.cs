using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Collision : MonoBehaviour
{
    void OnTriggerEnter(){
        print("Submarine has collided with ground surface");
    }
}
