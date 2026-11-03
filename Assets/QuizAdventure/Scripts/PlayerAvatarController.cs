using System;
using UnityEngine;
using UnityEngine.AI; // Wajib untuk membaca NavMeshAgent

// Update script ini dijalankan SETELAH MovableObjectMotor (order 0), supaya nilai Speed_f dari WASD
// tidak ditimpa motor sebelum Animator membacanya.
[DefaultExecutionOrder(100)]
public class PlayerAvatarController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float keyboardSpeed = 5f;
    public float timeToPause = 0.5f;

    // Referensi Komponen
    private Camera mainCam;
    private MovableObjectMotor objectMotor;
    private InputManager inputManager;
    public Animator anim;
    private NavMeshAgent agent;

    private LayerMask walkableMask;

    void Start()
    {
        // Mengambil semua komponen yang dibutuhkan dari karakter
        mainCam = Camera.main;
        objectMotor = GetComponent<MovableObjectMotor>();

        // MENGGUNAKAN GetComponentInChildren AGAR BISA MEMBACA ANIMATOR DI OBJEK ANAK/MODEL 3D
        anim = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();

        // Set LayerMask ke Layer 8 ("EnvironmentWalkable")
        walkableMask = 1 << 8;

        // Mencari InputManager secara otomatis di Scene
        inputManager = FindObjectOfType<InputManager>();

        if (inputManager != null)
        {
            inputManager.ClickDownEvent += OnClickDown;
            inputManager.EscEvent += OnEscPressed;
        }
        else
        {
            Debug.LogWarning("InputManager tidak ditemukan di scene! Pastikan ada GameObject bernama InputManagerObj.");
        }
    }

    void OnDestroy()
    {
        // Melepas event listener saat objek/script dihapus agar aman
        if (inputManager != null)
        {
            inputManager.ClickDownEvent -= OnClickDown;
            inputManager.EscEvent -= OnEscPressed;
        }
    }

    void Update()
    {
        HandleWASDMovement();
        UpdateAnimation();
    }

    private void HandleWASDMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(h, 0f, v).normalized;

        // Jika tombol WASD ditekan
        if (direction.magnitude >= 0.1f)
        {
            // Hentikan tujuan klik mouse agar tidak saling tarik-menarik
            if (agent != null && agent.hasPath)
            {
                agent.ResetPath();
            }

            // Gerakkan posisi fisik karakter
            Vector3 moveDirection = direction * keyboardSpeed * Time.deltaTime;
            // Pakai NavMeshAgent.Move supaya Player tetap di NavMesh (tidak menembus gedung)
            if (agent != null && agent.isOnNavMesh) agent.Move(moveDirection);
            else transform.Translate(moveDirection, Space.World);

            // Putar badan karakter sesuai arah pergerakan tombol WASD
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    private void UpdateAnimation()
    {
        if (anim == null) return;

        // MEMAKSA STATUS MENAPAK TANAH (GROUNDED) MENJADI TRUE
        anim.SetBool("Grounded_b", true);

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool isWASDPressed = (h != 0 || v != 0);

        // Cek apakah karakter sedang berjalan lewat klik mouse (NavMesh)
        bool isNavMeshMoving = (agent != null && agent.hasPath && agent.velocity.sqrMagnitude > 0.1f);

        // Jika WASD ditekan ATAU agen klik mouse sedang bergerak
        if (isWASDPressed || isNavMeshMoving)
        {
            anim.SetFloat("Speed_f", 1f); // Parameter yang sesuai di Animator
        }
        else
        {
            anim.SetFloat("Speed_f", 0f); // Kembali ke animasi Idle (Diam)
        }
    }

    private void OnClickDown(object source, EventArgs args)
    {
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, walkableMask))
        {
            if (objectMotor != null)
            {
                agent.SetDestination(hit.point);
            }
            else if (agent != null)
            {
                agent.SetDestination(hit.point);
            }
        }
    }

    private void OnEscPressed(object source, EventArgs args)
    {
        if (agent != null)
        {
            agent.ResetPath();
        }
    }
}