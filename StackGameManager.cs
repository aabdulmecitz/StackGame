using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class StackGameManager : MonoBehaviour
{
    [Header("Game Settings")]
    [Tooltip("Base movement speed of the blocks.")]
    public float movementSpeed = 5.0f;
    [Tooltip("Speed multiplier applied each stack.")]
    public float speedGain = 0.1f;
    [Tooltip("Maximum distance blocks travel from center.")]
    public float bounds = 5.0f;
    [Tooltip("Difference allowed to snap perfectly.")]
    public float perfectTolerance = 0.1f;

    [Header("Visuals")]
    public float cameraSmoothSpeed = 2.0f;
    public float colorChangeSpeed = 0.005f; // Decreased from 0.05f for smoother gradient
    public Material stackMat;
    public Material skyboxMaterial; 

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip placeClip;
    public AudioClip gameOverClip;
    public AudioClip comboClip;

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text gameOverScoreText; 
    public GameObject gameOverPanel;
    public GameObject menuPanel;

    private Transform currentBlock;
    private Transform previousBlock;
    
    private float currentSpeed;
    private bool isMovingOnX = true;
    private bool isGameActive = false;
    private bool isGameOver = false;

    private int score = 0;
    private int comboCount = 0;

    private List<Transform> blockStack = new List<Transform>();
    private const int MAX_VISIBLE_STACK = 25;

    private Vector3 currentBlockSize = new Vector3(5, 1, 5);
    private float currentHue = 0.0f;
    private Color currentBlockColor;

    private Camera mainCamera;
    private Vector3 cameraTargetPosition;
    private Renderer groundRenderer;
    private Material skyboxMat; 

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.8f, 0.8f, 0.8f);
            
            // FIX: Ensure no Skybox interferes with the Fog blending in Build
            RenderSettings.skybox = null; 
        }
        
        // FIX: Build might lack lighting/ambient setup, breaking the Fog blent.
        // 1. Force Flat Ambient Light so Ground isn't pitch black or weirdly lit
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.7f, 0.7f, 0.7f); // Bright enough grey

        // 2. Ensure we have a Main Light
        if (FindFirstObjectByType<Light>() == null)
        {
             GameObject lightGo = new GameObject("MainLight");
             Light l = lightGo.AddComponent<Light>();
             l.type = LightType.Directional;
             l.intensity = 1.0f;
             lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        currentSpeed = movementSpeed;
        
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
        
        if (scoreText != null) 
        {
            scoreText.text = "0";
            scoreText.gameObject.SetActive(false);
        }

        previousBlock = CreateBlock(new Vector3(0, -0.5f, 0), new Vector3(5, 1, 5));
        blockStack.Add(previousBlock); 
        
        currentBlockSize = new Vector3(5, 1, 5);
        
        currentHue = Random.Range(0f, 1f);
        UpdateBlockColor(previousBlock);

        if(mainCamera != null)
            cameraTargetPosition = mainCamera.transform.position;
            
        CreateGround();
    }

    void Update()
    {
        if (isGameOver)
        {
            DoGameOverZoom();

            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            return;
        }

        if (!isGameActive)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                isGameActive = true;
                if (menuPanel != null) menuPanel.SetActive(false);
                if (scoreText != null) scoreText.gameObject.SetActive(true);
                SpawnNewBlock();
            }
        }
        else
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                if (PlaceBlock())
                {
                    SpawnNewBlock();
                }
                else
                {
                    EndGame();
                }
            }

            MoveBlock();
            UpdateCamera();
        }
        
        UpdateVisuals();
    }

    private Transform CreateBlock(Vector3 position, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = position;
        go.transform.localScale = scale;
        
        Renderer rend = go.GetComponent<Renderer>();
        if (stackMat != null) 
        {
            rend.material = stackMat;
        }
        else
        {
            // FIX: Try URP Shader first, then others.
            Shader safeShader = Shader.Find("Universal Render Pipeline/Lit");
            if (safeShader == null) safeShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (safeShader == null) safeShader = Shader.Find("Mobile/Diffuse");
            if (safeShader == null) safeShader = Shader.Find("Legacy Shaders/Diffuse");
            if (safeShader != null) rend.material.shader = safeShader;
        }
        
        UpdateBlockColor(go.transform);

        return go.transform;
    }

    private void UpdateBlockColor(Transform t)
    {
        Renderer rend = t.GetComponent<Renderer>();
        if (rend != null)
        {
            currentBlockColor = Color.HSVToRGB(currentHue, 0.6f, 1.0f);
            rend.material.color = currentBlockColor;
        }
    }

    private void SpawnNewBlock()
    {
        if(scoreText != null) scoreText.text = score.ToString();

        isMovingOnX = !isMovingOnX;
        
        Vector3 spawnPos = previousBlock.position;
        float prevExtentY = previousBlock.localScale.y / 2.0f;
        float newExtentY = currentBlockSize.y / 2.0f;
        spawnPos.y += (prevExtentY + newExtentY);

        if (isMovingOnX) spawnPos.x = -bounds; 
        else             spawnPos.z = -bounds;

        currentHue += colorChangeSpeed;
        if (currentHue > 1.0f) currentHue -= 1.0f;

        currentBlock = CreateBlock(spawnPos, currentBlockSize);
    }

    private void MoveBlock()
    {
        if (currentBlock == null) return;

        float timeVal = Time.time * currentSpeed;
        float positionValue = Mathf.PingPong(timeVal, bounds * 2) - bounds;

        Vector3 pos = currentBlock.position;
        if (isMovingOnX) pos.x = positionValue;
        else pos.z = positionValue;

        currentBlock.position = pos;
    }

    private bool PlaceBlock()
    {
        Transform current = currentBlock;
        Transform prev = previousBlock;

        Vector3 currentPos = current.position;
        Vector3 prevPos = prev.position;

        float delta; 
        float size;
        
        if (isMovingOnX) { delta = currentPos.x - prevPos.x; size = current.localScale.x; }
        else             { delta = currentPos.z - prevPos.z; size = current.localScale.z; }

        float absDelta = Mathf.Abs(delta);

        if (absDelta >= size)
        {
            current.gameObject.AddComponent<Rigidbody>();
            PlaySound(gameOverClip, 1.0f);
            return false; 
        }

        if (absDelta <= perfectTolerance)
        {
            comboCount++;
            Vector3 snappedPos = current.position;
            if (isMovingOnX) snappedPos.x = prev.position.x;
            else snappedPos.z = prev.position.z;
            
            current.position = snappedPos;
            
            PlaySound(placeClip, 1.0f + (comboCount * 0.1f));
            CreateComboEffect(current.position, current.localScale);
        }
        else
        {
            comboCount = 0;
            PlaySound(placeClip, 1.0f);

            float newSize = size - absDelta;
            float rubbleSize = absDelta;

            Vector3 newScale = current.localScale;
            
            if (isMovingOnX)
            {
                newScale.x = newSize;
                current.localScale = newScale;
                
                float middle = prevPos.x + (delta / 2);
                current.position = new Vector3(middle, currentPos.y, currentPos.z);
                
                float rubbleX = (delta > 0) 
                    ? (current.position.x + newSize/2 + rubbleSize/2) 
                    : (current.position.x - newSize/2 - rubbleSize/2);

                SpawnRubble(
                    new Vector3(rubbleX, currentPos.y, currentPos.z),
                    new Vector3(rubbleSize, newScale.y, newScale.z),
                    true
                );
            }
            else 
            {
                newScale.z = newSize;
                current.localScale = newScale;
                
                float middle = prevPos.z + (delta / 2);
                current.position = new Vector3(currentPos.x, currentPos.y, middle);
               
                float rubbleZ = (delta > 0) 
                    ? (current.position.z + newSize/2 + rubbleSize/2) 
                    : (current.position.z - newSize/2 - rubbleSize/2);

                SpawnRubble(
                    new Vector3(currentPos.x, currentPos.y, rubbleZ),
                    new Vector3(newScale.x, newScale.y, rubbleSize),
                    false
                );
            }

            currentBlockSize = newScale;
        }

        previousBlock = current;
        blockStack.Add(current);
        
        score++;
        currentSpeed += speedGain;
        cameraTargetPosition = new Vector3(mainCamera.transform.position.x, current.position.y + 3.0f, mainCamera.transform.position.z); 

        return true;
    }

    private void SpawnRubble(Vector3 pos, Vector3 scale, bool isX)
    {
        GameObject rubble = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rubble.transform.position = pos;
        rubble.transform.localScale = scale;
        
        Renderer r = rubble.GetComponent<Renderer>();
        if(r != null) 
        {
            // FIX: Use the same material as the main blocks if available.
            // This prevents "Pink" issues in Editor if Mobile/Diffuse is incompatible.
            if (stackMat != null)
            {
                r.material = new Material(stackMat);
            }
            else
            {
                // FIX: Try URP Shader first, then others.
                Shader safeShader = Shader.Find("Universal Render Pipeline/Lit");
                if (safeShader == null) safeShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (safeShader == null) safeShader = Shader.Find("Mobile/Diffuse");
                if (safeShader == null) safeShader = Shader.Find("Legacy Shaders/Diffuse");
                if (safeShader != null) r.material.shader = safeShader;
            }
            
            r.material.color = currentBlockColor;
        }

        Rigidbody rb = rubble.AddComponent<Rigidbody>();
        rb.mass = 1f;

        rubble.AddComponent<RubbleControl>();

        Destroy(rubble, 2.0f);
    }
    
    private void CreateComboEffect(Vector3 pos, Vector3 scale)
    {
        GameObject pObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pObj.transform.position = pos;
        
        pObj.transform.localScale = new Vector3(scale.x, 0.1f, scale.z);
        
        Renderer r = pObj.GetComponent<Renderer>();
        if(r != null)
        {
            r.material = new Material(Shader.Find("Sprites/Default"));
            r.material.color = new Color(1f, 1f, 1f, 0.8f);
        }

        pObj.AddComponent<ComboEffect>();
    }

    void PlaySound(AudioClip clip, float pitch)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip);
        }
    }

    void UpdateVisuals()
    {
        if (mainCamera == null) return;
        
        Color targetColor = Color.HSVToRGB(currentHue, 0.2f, 0.9f); 
        mainCamera.backgroundColor = Color.Lerp(mainCamera.backgroundColor, targetColor, Time.deltaTime * 0.5f);
        
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = mainCamera.backgroundColor;

        // FIX: ArgumentNullException fix.
        // Instead of searching for shader at runtime (which might fail and crash),
        // we use the material you assign in the inspector slot.
        if (skyboxMaterial != null)
        {
            if (RenderSettings.skybox != skyboxMaterial)
                RenderSettings.skybox = skyboxMaterial;
            
            Color bg = mainCamera.backgroundColor;
            
            // Farklı Shader tipleri için renk eşitleme:
            if (skyboxMaterial.HasProperty("_Color")) skyboxMaterial.SetColor("_Color", bg);
            if (skyboxMaterial.HasProperty("_BaseColor")) skyboxMaterial.SetColor("_BaseColor", bg);
            if (skyboxMaterial.HasProperty("_SkyTint")) skyboxMaterial.SetColor("_SkyTint", bg);
            if (skyboxMaterial.HasProperty("_GroundColor")) skyboxMaterial.SetColor("_GroundColor", bg);
        }
        else
        {
            RenderSettings.skybox = null;
        }

        float stackH = (float)blockStack.Count;
        float offset = Mathf.Min(stackH * 0.5f, 50.0f); 
        
        RenderSettings.fogStartDistance = 30.0f + offset;
        RenderSettings.fogEndDistance = 80.0f + offset;
        
        mainCamera.farClipPlane = 120.0f + offset;
        
        if(QualitySettings.shadowDistance < 100) QualitySettings.shadowDistance = 120f;
    }

    void UpdateCamera()
    {
        if (mainCamera == null) return;
        Vector3 target = new Vector3(mainCamera.transform.position.x, cameraTargetPosition.y, mainCamera.transform.position.z);
        if(target.y < mainCamera.transform.position.y) target.y = mainCamera.transform.position.y;
        mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, target, Time.deltaTime * cameraSmoothSpeed);
    }

    void EndGame()
    {
        isGameOver = true;
        
        int best = PlayerPrefs.GetInt("HighScore", 0);
        if (score > best)
        {
            PlayerPrefs.SetInt("HighScore", score);
            best = score;
        }

        if (gameOverScoreText != null) gameOverScoreText.text = score.ToString();
        
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        
        if (scoreText != null) scoreText.gameObject.SetActive(false);
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void DoGameOverZoom()
    {
         if (mainCamera == null) return;

         float stackHeight = blockStack.Count; 
         if(stackHeight < 5) stackHeight = 5;

         float distance = 12f + (stackHeight * 0.5f);
         if(distance > 35f) distance = 35f;

         float targetY = Mathf.Min(stackHeight / 2.0f, 18.5f);
         Vector3 centerPoint = new Vector3(0, targetY, 0);
         
         Vector3 basePos = centerPoint - (mainCamera.transform.forward * distance);
         
         mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, basePos, Time.deltaTime * 1.0f);
    }

    private void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        
        ground.transform.position = new Vector3(0, -1.0f, 0);
        
        ground.transform.localScale = new Vector3(1000, 1, 1000);
        
        Renderer r = ground.GetComponent<Renderer>();
        if (r != null)
        {
            groundRenderer = r;
            
            // FIX: Even if user assigned a material, we MUST enforce a shader that supports Fog in build.
            // "Standard" shader often breaks Fog in builds if not properly referenced.
            // We clone their material but swap the shader to "Mobile/Diffuse".
            
            // FIX: Use assigned material if exists. Move shader fallback to ELSE block.
            if (stackMat != null)
            {
                r.material = new Material(stackMat);
            }
            else
            {
                // If no material assigned, try to find a valid shader.
                Shader safeShader = Shader.Find("Universal Render Pipeline/Lit");
                if (safeShader == null) safeShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (safeShader == null) safeShader = Shader.Find("Mobile/Diffuse");
                if (safeShader == null) safeShader = Shader.Find("Legacy Shaders/Diffuse");
                if (safeShader != null) r.material.shader = safeShader;
            }
            
            r.material.color = Color.white; 
        }
    }
}

public class RubbleControl : MonoBehaviour
{
    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.zero, Time.deltaTime * 1.5f);
    }
}

public class ComboEffect : MonoBehaviour
{
    private Material mat;
    private Color color;

    void Start()
    {
        Renderer r = GetComponent<Renderer>();
        if (r != null)
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; 
            r.receiveShadows = false; 

            mat = r.material; 
            if(mat.shader.name != "Sprites/Default") 
                mat.shader = Shader.Find("Sprites/Default");
                
            color = new Color(1f, 1f, 1f, 0.4f); 
            mat.color = color;
        }
        
        Destroy(gameObject, 0.5f);
    }

    void Update()
    {
        transform.localScale += new Vector3(1, 0, 1) * Time.deltaTime * 1.5f; 
        
        if (mat != null)
        {
            color.a -= Time.deltaTime; 
            if(color.a < 0) color.a = 0;
            mat.color = color;
        }
    }
}
