namespace UniVRM10
{
    public static class ControlRigRetarget
    {
        public static void Retarget(IControlRigGetter getter, IControlRigSetter setter)
        {
            foreach (var (head, parent) in setter.EnumerateBones())
            {
                var q = getter.GetNormalizedLocalRotation(head, parent);
                setter.SetNormalizedLocalRotation(head, q);
            }
            setter.SetRootPosition(getter.GetRootPosition());
        }
    }
}
