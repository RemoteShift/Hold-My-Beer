using System.Collections.Generic;
using UnityEngine;

/// <summary>Music is an attack for beer purposes. Overlapping songs use the strongest buff.</summary>
public class PianoPlayerEnemy : EnemyBrain
{
    private static readonly HashSet<PianoPlayerEnemy> Players = new HashSet<PianoPlayerEnemy>();
    public float musicRadius = 12f;
    [Header("Music Buffs — enable each effect independently")]
    [Tooltip("Navigation, including the keg's sprint. Does not affect a charger's attack dash.")]
    public EnemyBuffSetting moveSpeedBuff = new EnemyBuffSetting(true);
    [Tooltip("How quickly enemies turn to aim.")]
    public EnemyBuffSetting turnSpeedBuff = new EnemyBuffSetting();
    [Tooltip("Charger/haymaker windup and chair throw charging.")]
    public EnemyBuffSetting windupSpeedBuff = new EnemyBuffSetting(true);
    [Tooltip("Haymaker swing, charger dash, and chair projectile speed at launch. Excludes fuse and beer timers.")]
    public EnemyBuffSetting attackSpeedBuff = new EnemyBuffSetting(true);
    [Tooltip("Post-attack cooldown recovery speed.")]
    public EnemyBuffSetting recoverySpeedBuff = new EnemyBuffSetting(true);
    [Tooltip("Outgoing attack damage. Thrown furniture snapshots this when released.")]
    public EnemyBuffSetting damageBuff = new EnemyBuffSetting(false, 1.5f);
    [Tooltip("Divides incoming damage: 2 means half damage taken.")]
    public EnemyBuffSetting defenseBuff = new EnemyBuffSetting(false, 1.5f);
    [Tooltip("Multiplies maximum HP while preserving the current HP percentage on entry and exit.")]
    public EnemyBuffSetting maxHealthBuff = new EnemyBuffSetting(false, 1.5f);
    public AudioSource music;
    public Transform leftHand;
    public Transform rightHand;
    public bool IsPlayingMusic => isActiveAndEnabled && !IsDead && CurrentState is PlayingState;

    private void OnEnable() { Players.Add(this); }
    private void OnDisable() { Players.Remove(this); if (music) music.Pause(); }

    public static EnemyBuffs GetBuffs(EnemyBrain recipient)
    {
        var buffs = EnemyBuffs.None;
        foreach (var pianist in Players)
        {
            if (!pianist || pianist == recipient || !pianist.IsPlayingMusic) continue;
            if ((pianist.transform.position - recipient.transform.position).sqrMagnitude > pianist.musicRadius * pianist.musicRadius) continue;
            // Strongest enabled source wins per channel; unrelated effects can come from different pianists.
            buffs.moveSpeed = Mathf.Max(buffs.moveSpeed, pianist.moveSpeedBuff.Value);
            buffs.turnSpeed = Mathf.Max(buffs.turnSpeed, pianist.turnSpeedBuff.Value);
            buffs.windupSpeed = Mathf.Max(buffs.windupSpeed, pianist.windupSpeedBuff.Value);
            buffs.attackSpeed = Mathf.Max(buffs.attackSpeed, pianist.attackSpeedBuff.Value);
            buffs.recoverySpeed = Mathf.Max(buffs.recoverySpeed, pianist.recoverySpeedBuff.Value);
            buffs.damage = Mathf.Max(buffs.damage, pianist.damageBuff.Value);
            buffs.defense = Mathf.Max(buffs.defense, pianist.defenseBuff.Value);
            buffs.maxHealth = Mathf.Max(buffs.maxHealth, pianist.maxHealthBuff.Value);
        }
        return buffs;
    }

    public override void ReturnToDefaultState() => SwitchState(new PlayingState(this));
    private sealed class PlayingState : EnemyState
    {
        private readonly PianoPlayerEnemy pianist;
        private float songTime;
        private readonly Vector3 leftRest;
        private readonly Vector3 rightRest;
        public override bool IsAttacking => true;
        public PlayingState(PianoPlayerEnemy enemy) : base(enemy)
        {
            pianist = enemy;
            if (enemy.leftHand) leftRest = enemy.leftHand.localPosition;
            if (enemy.rightHand) rightRest = enemy.rightHand.localPosition;
        }
        public override void Enter()
        {
            pianist.StopPathfinding();
            pianist.SetBodyColor(new Color(0.55f, 0.25f, 0.7f));
            if (pianist.music)
            {
                if (songTime == 0f) pianist.music.Play(); else pianist.music.UnPause();
            }
        }
        public override void Exit() { if (pianist.music) pianist.music.Pause(); }
        public override void Update()
        {
            songTime += Time.deltaTime;
            if (pianist.leftHand) pianist.leftHand.localPosition = leftRest + Vector3.up * (Mathf.Sin(songTime * 18f) * 0.04f);
            if (pianist.rightHand) pianist.rightHand.localPosition = rightRest + Vector3.up * (Mathf.Cos(songTime * 18f) * 0.04f);
        }
    }
}
