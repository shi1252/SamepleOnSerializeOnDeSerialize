using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NewBehaviourScript : NetworkBehaviour {
    static float explosionDistance = 10.0f;

    public float moveSpeed = 10.0f;
    public float coolTime = 2f;

    float time;

    Vector3 vel;

    public bool isDead;

    Vector3 lerpPos = Vector3.zero;
    bool blerp = false;
    float lerpTime = 0.0f;

    void Start()
    {
        if (isLocalPlayer)
            GetComponent<Renderer>().material.color = Color.red;
        else
            GetComponent<Renderer>().material.color = Color.blue;
        if (isServer)
            StartCoroutine(SendPosVel());
        vel = GetComponent<Rigidbody>().velocity;
        time = coolTime;
        isDead = false;
    }

    [ClientCallback]
    void Update()
    {
        if (!isLocalPlayer || isDead)
            return;

        if (blerp)
        {
            lerpTime -= Time.deltaTime;
            transform.position = Vector3.Lerp(transform.position, lerpPos, 1 - lerpTime / 0.5f);
            if (lerpTime <= 0.0f)
                blerp = false;
        }

        Vector3 cam = transform.position;
        cam.y += 2;
        cam.z -= 15;
        Camera.main.transform.position = cam;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 vec = new Vector3(h, 0, v);
        if (Vector3.Distance(Vector3.zero, vec) > Vector3.kEpsilon)
            CmdMovement(vec);
        else
        {
            vel = Vector3.zero;
        }

        if (time >= coolTime)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                CmdExplosion();
                time = 0.0f;
            }
        }

        time += Time.deltaTime;
    }

    [ServerCallback]
    private void LateUpdate()
    {
        if (transform.position.y < -10.0f && !isDead)
        {
            isDead = true;
            SetDirtyBit(1u);
            GetOutMyWay(isDead);
        }
    }

    [Command]
    void CmdMovement(Vector3 v)
    {
        GetComponent<Rigidbody>().velocity += v * moveSpeed * Time.deltaTime;
        vel = v * moveSpeed * Time.deltaTime;
    }

    [Command]
    void CmdExplosion()
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag("Player");
        foreach (var obj in objs)
        {
            if (obj == this.gameObject)
                continue;
            float dis = Vector3.Distance(transform.position, obj.transform.position);
            if (dis <= explosionDistance)
            {
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb)
                {
                    rb.AddExplosionForce(1000.0f, transform.position, explosionDistance);
                }
            }
        }
    }

    void GetOutMyWay(bool d)
    {
        isDead = d;
        if (d)
        {
            StartCoroutine(Respawn());
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
        }
    }

    IEnumerator Respawn()
    {
        yield return new WaitForSeconds(5.0f);
        transform.position = new Vector3(0.0f, 10.0f, 0.0f);
        isDead = false;
        SetDirtyBit(1u);
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.velocity = Vector3.zero;
        rb.useGravity = true;
    }

    IEnumerator SendPosVel()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.1f);
            SetDirtyBit(2u);
        }
    }

    public override bool OnSerialize(NetworkWriter writer, bool initialState)
    {
        if (initialState)
        {
            writer.Write(isDead);
            writer.Write(transform.position);
            writer.Write(GetComponent<Rigidbody>().velocity);
            writer.Write(vel);
            return true;
        }
        bool wroteSyncVar = false;
        if ((base.syncVarDirtyBits & 1u) != 0u)
        {
            if (!wroteSyncVar)
            {
                writer.WritePackedUInt32(base.syncVarDirtyBits);
                wroteSyncVar = true;
            }
            writer.Write(isDead);
        }
        if ((base.syncVarDirtyBits & 2u) != 0u)
        {
            if (!wroteSyncVar)
            {
                writer.WritePackedUInt32(base.syncVarDirtyBits);
                wroteSyncVar = true;
            }
            writer.Write(transform.position);
            writer.Write(GetComponent<Rigidbody>().velocity);
            writer.Write(vel);
        }

        if (!wroteSyncVar)
        {
            writer.WritePackedUInt32(0);
        }
        return wroteSyncVar;
    }

    public override void OnDeserialize(NetworkReader reader, bool initialState)
    {
        if (initialState)
        {
            isDead = reader.ReadBoolean();
            transform.position = reader.ReadVector3();
            Vector3 rbv = reader.ReadVector3();
            vel = reader.ReadVector3();

            float time = (NetworkManager.singleton.client.GetRTT() * 0.001f * 0.5f);
            GetComponent<Rigidbody>().velocity = rbv + vel + vel * time;
            return;
        }
        int num = (int)reader.ReadPackedUInt32();
        if ((num & 1) != 0)
        {
            isDead = reader.ReadBoolean();
            if (!isDead)
                transform.position = new Vector3(0.0f, 10.0f, 0.0f);
        }
        if ((num & 2) != 0)
        {
            Vector3 pos = reader.ReadVector3();
            Vector3 rbv = reader.ReadVector3();
            vel = reader.ReadVector3();

            float time = (NetworkManager.singleton.client.GetRTT() * 0.001f * 0.5f);
            float dis = Vector3.Distance(transform.position, pos);
            if (dis >= 0.1f)
            {
                blerp = true;
                lerpTime = 0.5f;
                lerpPos = pos + rbv * time + 0.5f * vel * time * time;
            }
            GetComponent<Rigidbody>().velocity = rbv + vel + vel * time;
        }
    }
}