using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TestHyBirdClr;
public class HelloWorld : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {   
      
        Mytest.TestHyBirdClr();
        Debug.Log("代碼已經更新了哈哈哈\n" + "Hello World");
    }
     
    // Update is called once per frame
    void Update()
    {

    }
}
