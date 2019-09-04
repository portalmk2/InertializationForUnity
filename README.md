# Inertialization For Unity

Simple implementation of "Inertialization" algorithm in [High Performance Animation in Gears of War 4](https://cdn.gearsofwar.com/thecoalition/publications/SIGGRAPH%202017%20-%20High%20Performance%20Animation%20in%20Gears%20ofWar%204%20-%20Supplemental.pdf)


## Usage:
* Interpolate quaternion or vector by calling Pd.InertiaAlgorithm.InertializeXXX directly
* Or use PostInertializer behaviour. Specify an AvatarMask, and call PostInertializer.Trigger(\<BlendTime\>) to smooth out the transition

