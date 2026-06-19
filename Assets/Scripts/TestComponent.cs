using UnityEngine;

public class TestComponent : MonoBehaviour
{
    public int testInt;
    public GameObject testGameObject;

    private float testTimer;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(testTimer <= 5)
        {
            testTimer += Time.deltaTime;
        }
        else if(testGameObject != null)
        {
            testGameObject.SetActive(!testGameObject.activeSelf);
            testTimer = 0;
            testInt++;
        }
    }
}
