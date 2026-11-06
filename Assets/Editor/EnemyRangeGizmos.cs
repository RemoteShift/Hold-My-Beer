using UnityEditor;
using UnityEngine;

/// <summary>Selected-enemy diagnostics only; no editor dependencies or rendering in player builds.</summary>
public static class EnemyRangeGizmos
{
    private static readonly Color Detection = new Color(0.3f, 0.9f, 0.35f);
    private static readonly Color Attack = new Color(1f, 0.8f, 0.15f);
    private static readonly Color Damage = new Color(1f, 0.25f, 0.15f);
    private static readonly Color Pickup = new Color(0.15f, 0.8f, 1f);
    private static readonly Color Music = new Color(0.8f, 0.4f, 1f);

    [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy)]
    private static void Draw(EnemyBrain enemy, GizmoType gizmoType)
    {
        var oldColor = Gizmos.color;
        var oldMatrix = Gizmos.matrix;
        var oldHandlesColor = Handles.color;
        var oldHandlesMatrix = Handles.matrix;
        try
        {
            // Combat ranges use world distances, regardless of prefab scale.
            Gizmos.matrix = Matrix4x4.identity;
            Handles.matrix = Matrix4x4.identity;
            var origin = enemy.transform.position;
            var forward = Vector3.ProjectOnPlane(enemy.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

            // Pianists play continuously; their inherited detection stat is not used.
            if (!(enemy is PianoPlayerEnemy)) Sphere(origin, enemy.detectRange, Detection, "Detection");
            Arrow(origin, origin + forward * 1.5f, Color.white);
            var status = enemy.IsBeered ? $"BEERED — {enemy.BeerSecondsLeft:0.0}s" :
                enemy.CurrentState != null ? enemy.CurrentState.GetType().Name : "Facing";
            Label(origin + Vector3.up * 2.5f, enemy.name + "\n" + status + $"\nMass {enemy.mass:0.##}", enemy.IsBeered ? Attack : Color.white);

            switch (enemy)
            {
                case HaymakerEnemy haymaker:
                    Sphere(origin, haymaker.attackRange, Attack, "Start swing");
                    var start = Quaternion.AngleAxis(-haymaker.swingArc * 0.5f, Vector3.up) * forward;
                    var end = Quaternion.AngleAxis(haymaker.swingArc * 0.5f, Vector3.up) * forward;
                    Handles.color = new Color(Damage.r, Damage.g, Damage.b, 0.08f);
                    Handles.DrawSolidArc(origin, Vector3.up, start, haymaker.swingArc, haymaker.swingRadius);
                    Handles.color = Damage;
                    Handles.DrawWireArc(origin, Vector3.up, start, haymaker.swingArc, haymaker.swingRadius);
                    Gizmos.color = Damage;
                    Gizmos.DrawLine(origin, origin + start * haymaker.swingRadius);
                    Gizmos.DrawLine(origin, origin + end * haymaker.swingRadius);
                    Label(origin + forward * haymaker.swingRadius,
                        $"Swing {haymaker.swingRadius:0.#}m / {haymaker.swingArc:0.#}°", Damage);
                    if (haymaker.swingPivot)
                        Arrow(origin, origin + haymaker.swingPivot.forward * haymaker.swingRadius, Attack);
                    break;
                case ChargerEnemy charger:
                    Ring(origin, charger.attackRange, Attack, "Start charge");
                    var data = new SerializedObject(charger);
                    var radius = data.FindProperty("hitRadius").floatValue;
                    var offset = data.FindProperty("hitForwardOffset").floatValue;
                    var hitCenter = origin + charger.transform.forward * offset;
                    Sphere(hitCenter, radius, Damage, "Hit radius");
                    var finish = hitCenter + forward * charger.chargeDistance;
                    var side = Vector3.Cross(Vector3.up, forward) * radius;
                    Gizmos.color = Damage;
                    Gizmos.DrawLine(hitCenter + side, finish + side);
                    Gizmos.DrawLine(hitCenter - side, finish - side);
                    Gizmos.DrawWireSphere(finish, radius);
                    Arrow(hitCenter, finish, Damage);
                    Label(finish + Vector3.up * 0.3f, $"Max charge {charger.chargeDistance:0.#}m", Damage);
                    break;
                case ChairThrowerEnemy thrower:
                    Sphere(origin, thrower.throwRange, Attack, "Start throw");
                    Sphere(origin, thrower.pickupRadius, Pickup, "Find furniture");
                    Sphere(origin, thrower.pickupReach, Pickup * 0.8f, "Pickup reach", Vector3.left);
                    DrawThrow(thrower, forward);
                    break;
                case PowderKegEnemy keg:
                    Sphere(origin, keg.explosionRadius, Damage, "Blast");
                    Label(origin + Vector3.up * 1.8f,
                        keg.IsLit ? $"Fuse {keg.FuseRemaining:0.0}s{(keg.IsBeered ? " (paused)" : "")}" :
                        $"Fuse {keg.fuseSeconds:0.#}s on detection", Damage);
                    break;
                case PianoPlayerEnemy pianist:
                    var color = Application.isPlaying && !pianist.IsPlayingMusic ? Color.gray : Music;
                    Sphere(origin, pianist.musicRadius, color, "Music aura");
                    Label(origin + Vector3.up * 1.8f,
                        $"Move ×{pianist.moveSpeedBuff.Value:0.##} | Windup ×{pianist.windupSpeedBuff.Value:0.##}\n" +
                        $"Attack ×{pianist.attackSpeedBuff.Value:0.##} | Recovery ×{pianist.recoverySpeedBuff.Value:0.##}\n" +
                        $"Damage ×{pianist.damageBuff.Value:0.##} | Defense ×{pianist.defenseBuff.Value:0.##} | HP ×{pianist.maxHealthBuff.Value:0.##}" +
                        (Application.isPlaying && !pianist.IsPlayingMusic ? " (inactive)" : ""), color);
                    break;
            }
        }
        finally
        {
            Gizmos.color = oldColor;
            Gizmos.matrix = oldMatrix;
            Handles.color = oldHandlesColor;
            Handles.matrix = oldHandlesMatrix;
        }
    }

    private static void DrawThrow(ChairThrowerEnemy thrower, Vector3 forward)
    {
        var origin = thrower.chairHoldPoint ? thrower.chairHoldPoint.position : thrower.transform.position + Vector3.up;
        var attacking = Application.isPlaying && (thrower.CanBeBeered || thrower.IsBeered);
        var offset = attacking ? thrower.ThrowAimOffset :
            thrower.target ? thrower.target.position - origin :
            thrower.transform.position + forward * thrower.throwRange - origin;
        var duration = Mathf.Max(0.1f, thrower.flightSeconds) / thrower.Buffs.attackSpeed;
        var velocity = offset / duration - Physics.gravity * (duration * 0.5f);
        var previous = origin;
        Gizmos.color = Damage;
        for (var i = 1; i <= 32; i++)
        {
            var time = duration * i / 32f;
            var point = origin + velocity * time + Physics.gravity * (0.5f * time * time);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
        Gizmos.DrawWireSphere(origin, 0.12f);
        Gizmos.DrawWireSphere(previous, 0.25f);
        Label(previous, $"{(attacking ? "Attack trajectory" : "Throw preview")} ({duration:0.#}s)\nUnobstructed path", Damage);
    }

    private static void Sphere(Vector3 origin, float radius, Color color, string text, Vector3 direction = default)
    {
        radius = Mathf.Max(0f, radius);
        Gizmos.color = color;
        Gizmos.DrawWireSphere(origin, radius);
        Label(origin + (direction == Vector3.zero ? Vector3.right : direction) * radius,
            $"{text} {radius:0.#}m", color);
    }

    private static void Ring(Vector3 origin, float radius, Color color, string text)
    {
        Handles.color = color;
        Handles.DrawWireDisc(origin, Vector3.up, Mathf.Max(0f, radius));
        Label(origin + Vector3.right * radius, $"{text} {radius:0.#}m", color);
    }

    private static void Arrow(Vector3 from, Vector3 to, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawLine(from, to);
        var direction = (to - from).normalized;
        var side = Vector3.Cross(direction, Vector3.up).normalized;
        Gizmos.DrawLine(to, to - direction * 0.35f + side * 0.2f);
        Gizmos.DrawLine(to, to - direction * 0.35f - side * 0.2f);
    }

    private static void Label(Vector3 position, string text, Color color)
    {
        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = color;
        Handles.Label(position, text, style);
    }
}
