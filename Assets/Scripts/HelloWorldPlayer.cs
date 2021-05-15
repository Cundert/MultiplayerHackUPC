using MLAPI;
using MLAPI.Messaging;
using MLAPI.NetworkVariable;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace HelloWorld {
	public class HelloWorldPlayer : NetworkBehaviour {
		public List<Vector2> points;
	  
		public Vector2 dir, adir;
		public int nattacks;
		[HideInInspector]
		public float speed; // Speed of the player
		public int maxHp;

		public CameraController playerCamera;
		public GameObject cameraFocus;
		public GameObject lastAttacker;

		public List<HelloWorldPlayer> kills = new List<HelloWorldPlayer>();

		[HideInInspector]
		public float attackDelay; // Delay between attacks

		public float lastAttack = 0.0f;
		public float Timer = 0.0f;

		public GameObject AttackObject;

		public int amountOfObstacleCollisions = 0;
		public bool isDead;

		// Variable stats
		public NetworkVariableFloat variableSpeed = new NetworkVariableFloat();
		public NetworkVariableFloat variableAttackDelay = new NetworkVariableFloat();


		public NetworkVariableString PlayerName = new NetworkVariableString(new NetworkVariableSettings
		{
			WritePermission = NetworkVariablePermission.ServerOnly,
			ReadPermission = NetworkVariablePermission.Everyone
		});

		public NetworkVariableVector3 Position = new NetworkVariableVector3(new NetworkVariableSettings
		{
			WritePermission = NetworkVariablePermission.ServerOnly,
			ReadPermission = NetworkVariablePermission.Everyone
		});

		public NetworkVariableVector3 AttackDir = new NetworkVariableVector3(new NetworkVariableSettings
		{
			WritePermission = NetworkVariablePermission.ServerOnly,
			ReadPermission = NetworkVariablePermission.Everyone
		});

		public NetworkVariableInt NAttacks = new NetworkVariableInt(new NetworkVariableSettings
		{
			WritePermission = NetworkVariablePermission.ServerOnly,
			ReadPermission = NetworkVariablePermission.Everyone
		});

		public NetworkVariableInt HP = new NetworkVariableInt(new NetworkVariableSettings
		{
			WritePermission = NetworkVariablePermission.ServerOnly,
			ReadPermission = NetworkVariablePermission.Everyone
		});

		public NetworkVariableBool HPHasBeenSet = new NetworkVariableBool(new NetworkVariableSettings
		{
			WritePermission = NetworkVariablePermission.ServerOnly,
			ReadPermission = NetworkVariablePermission.Everyone
		});

		private void OnEnable()
		{
			PlayerName.OnValueChanged += DisplayNames;
			DontDestroyOnLoad(this.gameObject);
			attackDelay=0.75f;
			speed=3.0f;
			maxHp=10;
		}

		private void OnDisable()
		{
			PlayerName.OnValueChanged -= DisplayNames;
		}

		public override void NetworkStart()
		{
			Move();
			InitializeHPServerRpc(maxHp);
		}

    public void SetPosition(Vector3 pos) {
			if (!IsLocalPlayer) return;
			transform.position=pos;
			UpdatePositionServerRpc(transform.position);
		}
    
		public void Move()
		{
			if (!IsLocalPlayer) return;
			transform.position += new Vector3(dir.x, dir.y, 0);
			UpdatePositionServerRpc(transform.position);
		}

		public void Attack()
		{
			if (!IsLocalPlayer) return;
			if (adir.x == 0 && adir.y == 0) return;
			if (lastAttack + attackDelay > Timer) return;
			lastAttack = Timer;
			UpdateAttackServerRpc(adir);
		}

		void DisplayNames(string before, string after)
		{
			string childname = transform.GetChild(0).gameObject.GetComponent<TextMesh>().text;
			if (childname != after)
			{
				transform.GetChild(0).gameObject.GetComponent<TextMesh>().text = after;
			}
		}

		[ServerRpc]
		public void SetPlayerSpeedServerRpc(float speed, ServerRpcParams rpcParams = default) {
			variableSpeed.Value=speed;
		}

		[ServerRpc]
		public void SetPlayerAttackDelayServerRpc(float delay, ServerRpcParams rpcParams = default) {
			variableAttackDelay.Value=delay;
		}

		[ServerRpc]
		public void SetPlayerNameServerRpc(string name, ServerRpcParams rpcParams = default)
		{
			PlayerName.Value = name;
		}

		[ServerRpc]
		void UpdatePositionServerRpc(Vector3 d, ServerRpcParams rpcParams = default)
		{
			Position.Value = d;
		}

		[ServerRpc]
		void UpdateAttackServerRpc(Vector3 a, ServerRpcParams rpcParams = default)
		{
			AttackDir.Value = a;
			NAttacks.Value++;
		}

		[ServerRpc]
		void InitializeHPServerRpc(int InitialHP, ServerRpcParams rpcParams = default)
		{
			HP.Value = InitialHP;
			HPHasBeenSet.Value = true;
		}

		[ServerRpc]
		void UpdateHPServerRpc(int HPDiff, ServerRpcParams rpcParams = default)
		{
			HP.Value += HPDiff;
		}

		void generateAttack()
		{
			// Generates an attack in direction AttackDir
			// todo
			GameObject bullet = Instantiate(AttackObject, transform.position, Quaternion.FromToRotation(new Vector3(1, 0, 0), AttackDir.Value));
			bullet.GetComponent<BulletScript>().source = gameObject;
		}


		Vector2 getMovementVector(float val)
		{
			Vector2 dir = new Vector2(0, 0);
			if (Input.GetKey("s")) dir += new Vector2(0, -1);
			if (Input.GetKey("w")) dir += new Vector2(0, 1);
			if (Input.GetKey("a")) dir += new Vector2(-1, 0);
			if (Input.GetKey("d")) dir += new Vector2(1, 0);

			if (Physics2D.CircleCast(
					transform.position,
					GetComponent<CircleCollider2D>().radius,
					new Vector3(dir.x, 0, 0),
					val,
					LayerMask.GetMask("Wall")
				).collider != null)
				dir.x = 0;
			if (Physics2D.CircleCast(
					transform.position,
					GetComponent<CircleCollider2D>().radius,
					new Vector3(0, dir.y, 0),
					val,
					LayerMask.GetMask("Wall")
				).collider != null)
				dir.y = 0;

			dir.Normalize();
			return dir * val;
		}

		Vector2 getAttackVector()
		{
			adir = new Vector2(0, 0);
			if (Input.GetKey("down")) adir += new Vector2(0, -1);
			if (Input.GetKey("up")) adir += new Vector2(0, 1);
			if (Input.GetKey("left")) adir += new Vector2(-1, 0);
			if (Input.GetKey("right")) adir += new Vector2(1, 0);
			return adir;
		}

		void MoveCamera()
		{
			Vector3 serverPosition = cameraFocus.GetComponent<HelloWorldPlayer>().transform.position;
			Vector3 possibleFuturePosition = new Vector3(serverPosition.x, serverPosition.y, -10);
			playerCamera.transform.position = possibleFuturePosition;
		}

		void ChangeCameraFocus(GameObject focus)
		{
			cameraFocus = focus;
		}

		void UpdateMyKillsCamera(GameObject focus)
		{
			foreach (HelloWorldPlayer ded in kills)
			{
				ded.ChangeCameraFocus(focus);
				ded.UpdateMyKillsCamera(focus);
			}
		}

		void Die()
		{
			if (IsLocalPlayer)
			{
				ChangeCameraFocus(lastAttacker);
			}
			UpdateMyKillsCamera(lastAttacker);
			lastAttacker.GetComponent<HelloWorldPlayer>().kills.Add(this);
			gameObject.layer = 6;
			GetComponent<SpriteRenderer>().enabled = false;
			transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
			isDead = true;
		}

		void loadPoints(){
			points = MapScript.instance.points;
		}
		
		void DrawRays(){
			foreach(Vector2 v in points){
				LayerMask mask = LayerMask.GetMask("BulletWall");
				RaycastHit2D hit = Physics2D.Raycast(transform.position, v - new Vector2(transform.position.x, transform.position.y), 2000.0f, mask);
				if (hit != null){
					Debug.DrawLine(transform.position, hit.point, Color.white);
				}
			}
		}

		void Start()
		{
			isDead = false;
			playerCamera = CameraController.instance;
			cameraFocus = gameObject;
      HelloWorldManager.players.Add(this);
		}


		void Update()
		{
			if (!isDead && HPHasBeenSet.Value && HP.Value == 0)
				Die();
			while (!isDead && nattacks < NAttacks.Value)
			{
				++nattacks;
				generateAttack();
			}

			if (IsLocalPlayer)
			{
				if (!isDead)
				{
					Timer += Time.deltaTime;

					dir = getMovementVector(speed * Time.deltaTime);
					adir = getAttackVector();

					Move();
					Attack();
				}
				MoveCamera();
			}
			else
			{
				if (!isDead) {
					transform.position=Position.Value;
					speed=variableSpeed.Value;
					attackDelay=variableAttackDelay.Value;
				}
			}
		}
		
		void FixedUpdate(){
			loadPoints();
			DrawRays();
		}


		void OnTriggerEnter2D(Collider2D other)
		{
			// If the Player has collided with a bullet that isn't theirs, and the Player is the local one
			// Then reduce the Player's health
			if (!isDead && other.gameObject.tag == "Bullet" && other.GetComponent<BulletScript>().source != gameObject)
			{
				lastAttacker = other.GetComponent<BulletScript>().source;
				if (IsLocalPlayer) UpdateHPServerRpc(other.GetComponent<BulletScript>().BulletDamage * -1);
			}

			if (!isDead && other.gameObject.tag=="Item") {
				if (IsLocalPlayer) {
					other.GetComponent<PickableObject>().PickItem(this);
					// Subir stats de local a server
					SetPlayerSpeedServerRpc(speed);
					SetPlayerAttackDelayServerRpc(attackDelay);
					// La bajada de stats de server a local se hace en update para evitar problemas de sincronizacion
				}
			}
		}
	}
}