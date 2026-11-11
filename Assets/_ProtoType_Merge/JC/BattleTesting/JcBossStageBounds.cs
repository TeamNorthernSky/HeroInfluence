using UnityEngine;

namespace JC.BattleTesting
{
    // The visible area is the union of the floor and two window walls. As exit faces of
    // one convex box their projected interiors never overlap, even at the hall corner.
    internal sealed class JcBossStageBounds
    {
        readonly Transform stage;
        readonly Vector3[] vertices = new Vector3[12], scratch = new Vector3[12];
        Matrix4x4 toView;
        float horizontal, vertical, near;

        public JcBossStageBounds(Transform stage) { this.stage = stage; }

        public bool Covers(Pose pose, float fov, float aspect, float nearClip, float right)
        {
            toView = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one).inverse * stage.localToWorldMatrix;
            vertical = Mathf.Tan(.5f * fov * Mathf.Deg2Rad);
            horizontal = vertical * aspect;
            near = nearClip;
            var eye = stage.InverseTransformPoint(pose.position);
            const float left = -23f, front = -48f, back = 25f, top = 20.8f;
            float area = 0f;
            if (eye.y > 0)
                area += Face(new Vector3(left,0,front),new Vector3(right,0,front),new Vector3(right,0,back),new Vector3(left,0,back));
            if (eye.x > left)
                area += Face(new Vector3(left,0,front),new Vector3(left,0,back),new Vector3(left,top,back),new Vector3(left,top,front));
            if (eye.z < back)
                area += Face(new Vector3(left,0,back),new Vector3(right,0,back),new Vector3(right,top,back),new Vector3(left,top,back));
            return area >= 3.99999f; // NDC rectangle [-1,1]², with roundoff tolerance only.
        }

        float Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var input = vertices;
            var output = scratch;
            input[0]=toView.MultiplyPoint3x4(a); input[1]=toView.MultiplyPoint3x4(b);
            input[2]=toView.MultiplyPoint3x4(c); input[3]=toView.MultiplyPoint3x4(d);
            int count=4;
            for (int plane=0;plane<5;plane++)
            {
                var prev=input[count-1];
                float prevDistance=Distance(prev,plane);
                int written=0;
                for (int i=0;i<count;i++)
                {
                    var next=input[i];
                    float nextDistance=Distance(next,plane);
                    if ((prevDistance>=0)!=(nextDistance>=0))
                        output[written++]=Vector3.LerpUnclamped(prev,next,prevDistance/(prevDistance-nextDistance));
                    if (nextDistance>=0) output[written++]=next;
                    prev=next; prevDistance=nextDistance;
                }
                if (written<3) return 0;
                count=written; var swap=input; input=output; output=swap;
            }
            float twiceArea=0;
            var last=input[count-1];
            for (int i=0;i<count;i++)
            {
                var p=input[i];
                twiceArea+=(last.x*p.y-last.y*p.x)/(last.z*p.z);
                last=p;
            }
            return Mathf.Abs(twiceArea)*.5f/(horizontal*vertical);
        }

        float Distance(Vector3 p,int plane)
        {
            switch (plane)
            {
                case 0: return p.z-near;
                case 1: return p.x+p.z*horizontal;
                case 2: return p.z*horizontal-p.x;
                case 3: return p.y+p.z*vertical;
                default: return p.z*vertical-p.y;
            }
        }

        public bool Allows(Pose pose, float fov, float aspect, float nearClip, float right) =>
            Covers(pose,fov+.05f,aspect,nearClip,right);

        public Pose EntryPose(Pose requested, float fov, float aspect, float nearClip, float right, Vector3 center, out float entryFov)
        {
            entryFov = fov;
            if (Allows(requested,fov,aspect,nearClip,right)) return requested;
            var angles = requested.rotation.eulerAngles;
            for (int degrees=1; degrees<=30; degrees++)
            {
                var pitched = new Pose(requested.position,Quaternion.Euler(angles.x+degrees,angles.y,angles.z));
                if (Allows(pitched,fov,aspect,nearClip,right)) return pitched;
                var turned = new Pose(requested.position,Quaternion.Euler(angles.x,angles.y-degrees,angles.z));
                if (Allows(turned,fov,aspect,nearClip,right)) return turned;
            }

            // Only needed before the first shot, when there is no previous in-bounds pose.
            var local = stage.InverseTransformPoint(center);
            local.x=Mathf.Clamp(local.x,-15f,right-2f);
            local.z=Mathf.Clamp(local.z,-30f,10f);
            local.y=32f;
            float span=.95f*Mathf.Min(right-local.x,local.x+23f,(25f-local.z)/aspect,(local.z+48f)/aspect);
            entryFov=2f*Mathf.Atan(span/local.y)*Mathf.Rad2Deg;
            return new Pose(stage.TransformPoint(local),stage.rotation*Quaternion.Euler(90,90,0));
        }

        // Travel only along the requested path. Stop at its first boundary rather than selecting a different shot.
        public float AllowedFraction(Pose start, float startFov, Pose end, float endFov, float aspect, float nearClip, float right)
        {
            float distance=Vector3.Distance(start.position,end.position);
            float angle=Quaternion.Angle(start.rotation,end.rotation);
            int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(distance/.5f,angle/2f,Mathf.Abs(endFov-startFov)/2f)));
            bool Allowed(float t) => Allows(Interpolate(start,end,t),Mathf.Lerp(startFov,endFov,t),aspect,nearClip,right);
            for (int i=1;i<=steps;i++)
            {
                float hi=(float)i/steps;
                if (Allowed(hi)) continue;
                float lo=(float)(i-1)/steps;
                for (int j=0;j<10;j++)
                {
                    float mid=(lo+hi)*.5f;
                    if (Allowed(mid)) lo=mid; else hi=mid;
                }
                return lo;
            }
            return 1f;
        }

        public static Pose Interpolate(Pose a, Pose b, float t) =>
            new Pose(Vector3.Lerp(a.position,b.position,t),Quaternion.Slerp(a.rotation,b.rotation,t));
    }
}
