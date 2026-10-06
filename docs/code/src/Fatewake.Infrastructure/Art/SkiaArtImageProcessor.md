# SkiaArtImageProcessor

Cross-platform SkiaSharp implementation. Accepts PNG masters only, limits images to 16,384 pixels per dimension and 100 million total pixels, preserves alpha, emits WebP at quality 85 and lossless PNG.

The master PNG remains immutable; processing creates delivery derivatives only. The implementation is supported on both Linux containers and Windows hosts.
