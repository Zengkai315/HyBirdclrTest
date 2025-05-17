using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TestHyBirdClr
{
    public class Mytest : MonoBehaviour
    {

        public static void TestHyBirdClr()
        {
            Debug.Log("我是MyTest类->TestHyBirdClr");
        }
        // Start is called before the first frame update
        void Start()
        {   
            Debug.Log("我是MyTest类->Start()");
        }
        
        void OnEnable()
        {
            Debug.Log("我是MyTest类->OnEnable");
        }
        
        void OnDisable()
        {
            Debug.Log("我是MyTest类->OnDisable");
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    } 
}

