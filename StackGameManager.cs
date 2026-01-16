/*
 * Copyright (c) 2026 Ahmet [Last Name]
 * All rights reserved.
 * 
 * This code is proprietary and confidential. 
 * Unauthorized copying of this file, via any medium is strictly prohibited.
 */

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro; // TextMeshPro namespace
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
    public float colorChangeSpeed = 0.05f;
    public Material stackMat; // Assign a material with "Standard" shader or similar for coloring

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip placeClip;
    public AudioClip gameOverClip;
    public AudioClip comboClip; // Optional, or just use placeClip with high pitch

    [Header("UI")]
    public TMP_Text scoreText;
    public GameObject gameOverPanel;
    public GameObject menuPanel; // A specific panel for "Tap to Start"

    // State
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

    // ... (rest of the file remains same, replacing specific methods) ...

    void Start()
    {
        mainCamera = Camera.main;
        currentSpeed = movementSpeed;
        
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
        if (scoreText != null) scoreText.text = "0";

        previousBlock = CreateBlock(new Vector3(0, -0.5f, 0), new Vector3(5, 1, 5));
        blockStack.Add(previousBlock); 
        
        currentBlockSize = new Vector3(5, 1, 5);
        
        currentHue = Random.Range(0f, 1f);
        UpdateBlockColor(previousBlock);

        if(mainCamera != null)
            cameraTargetPosition = mainCamera.transform.position;

        // Create Background Particles
        CreateBackgroundParticles();
    }

    // ... (Update and other methods) ...



    // New Background Elements
    private void CreateBackgroundParticles()
    {
        GameObject bgSys = new GameObject("BackgroundStars");
        ParticleSystem ps = bgSys.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 10f;
        main.startSpeed = 2f;
        main.startSize = 0.5f;
        main.maxParticles = 100;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // Move relative to world

        var emission = ps.emission;
        emission.rateOverTime = 10;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(30, 1, 30); // Spread out
        
        // Position it below camera initially, or around
        bgSys.transform.position = new Vector3(0, -10, 10);
        bgSys.transform.rotation = Quaternion.Euler(-90, 0, 0); // Emit upwards
        
        var renderer = bgSys.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
    }


    void Update()
    {
        if (isGameOver)
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            return;
        }

        if (!isGameActive)
        {
            // Waiting for start
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
            // Gameplay
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
        
        // Assign material for batching/shadows if needed, or just default
        Renderer rend = go.GetComponent<Renderer>();
        if (stackMat != null) rend.material = stackMat;
        
        UpdateBlockColor(go.transform); // Apply current game color

        return go.transform;
    }

    private void UpdateBlockColor(Transform t)
    {
        Renderer rend = t.GetComponent<Renderer>();
        if (rend != null)
        {
            // Use HSV for nice transitions
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

        // Visual change first
        currentHue += colorChangeSpeed;
        if (currentHue > 1.0f) currentHue -= 1.0f;

        // Pooling Check
        if (blockStack.Count > MAX_VISIBLE_STACK)
        {
            currentBlock = blockStack[0];
            blockStack.RemoveAt(0);

            currentBlock.position = spawnPos;
            currentBlock.localScale = currentBlockSize;
            currentBlock.rotation = Quaternion.identity;
            currentBlock.gameObject.GetComponent<Renderer>().enabled = true; // Ensure visible
            
            UpdateBlockColor(currentBlock);
        }
        else
        {
            currentBlock = CreateBlock(spawnPos, currentBlockSize);
        }
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

        // 1. Miss
        if (absDelta >= size)
        {
            current.gameObject.AddComponent<Rigidbody>();
            PlaySound(gameOverClip, 1.0f);
            return false; 
        }

        // 2. Perfect Hit
        if (absDelta <= perfectTolerance)
        {
            comboCount++;
            Vector3 snappedPos = current.position;
            if (isMovingOnX) snappedPos.x = prev.position.x;
            else snappedPos.z = prev.position.z;
            
            current.position = snappedPos;
            
            // Visual + Audio effects
            PlaySound(placeClip, 1.0f + (comboCount * 0.1f)); // Pitch increases
            CreateComboEffect(current.position, current.localScale);
            
            // Allow stack to grow slightly on perfect? (Optional, kept simple here)
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
        if(r != null) r.material.color = currentBlockColor;

        Rigidbody rb = rubble.AddComponent<Rigidbody>();
        rb.mass = 1f;

        // Shrink rubble over time script
        rubble.AddComponent<RubbleControl>();

        Destroy(rubble, 2.0f);
    }
    
    // Simple visual flare for combo
    // Simple visual flare for combo
    private void CreateComboEffect(Vector3 pos, Vector3 scale)
    {
        GameObject pObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pObj.transform.position = pos;
        
        // Make it slightly larger than the block and very thin
        pObj.transform.localScale = new Vector3(scale.x, 0.1f, scale.z);
        
        Renderer r = pObj.GetComponent<Renderer>();
        if(r != null)
        {
            r.material = new Material(Shader.Find("Sprites/Default"));
            r.material.color = new Color(1f, 1f, 1f, 0.8f); // Semi-transparent white
        }

        // Add the effect (expanding and fading)
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
        
        // Fog removed as requested
        RenderSettings.fog = false;
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

        if (scoreText != null) scoreText.text = score.ToString();
        
        // UI Handling
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (scoreText != null) scoreText.gameObject.SetActive(false); // Hide in-game score so user focuses on Game Over panel
    }
}

// ---- HELPER CLASSES (Can be in same file) ----

public class RubbleControl : MonoBehaviour
{
    void Update()
    {
        // Shrink over time
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
            mat = r.material;
            color = mat.color;
        }
        else
        {
            // Failsafe
            Destroy(gameObject);
        }
        
        Destroy(gameObject, 1.0f);
    }

    void Update()
    {
        // Expand X and Z only
        transform.localScale += new Vector3(1, 0, 1) * Time.deltaTime * 5.0f;
        
        // Fade out
        if (mat != null)
        {
            color.a -= Time.deltaTime * 1.5f; // Fade faster than life
            mat.color = color;
        }
    }
}
