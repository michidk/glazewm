/// Dimensions that a native window refuses to resize in either direction.
/// A window may have one fixed dimension and one freely resizable
/// dimension.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct FixedWindowSize {
  pub width: Option<i32>,
  pub height: Option<i32>,
}

impl FixedWindowSize {
  /// Only accept a fixed dimension when both probes agree with the
  /// original size. A one-sided refusal is a minimum/maximum, not a
  /// fixed dimension.
  #[must_use]
  pub fn from_probes(
    original: (i32, i32),
    larger: (i32, i32),
    smaller: (i32, i32),
    probe_width: bool,
    probe_height: bool,
  ) -> Self {
    Self {
      width: (probe_width
        && original.0 == larger.0
        && original.0 == smaller.0)
        .then_some(original.0),
      height: (probe_height
        && original.1 == larger.1
        && original.1 == smaller.1)
        .then_some(original.1),
    }
  }
}

#[cfg(test)]
mod tests {
  use super::*;

  #[test]
  fn detects_fixed_width_without_freezing_height() {
    assert_eq!(
      FixedWindowSize::from_probes(
        (740, 1000),
        (740, 1100),
        (740, 900),
        true,
        true,
      ),
      FixedWindowSize {
        width: Some(740),
        height: None
      },
    );
  }

  #[test]
  fn one_sided_size_limits_are_not_fixed() {
    for (larger, smaller) in [
      ((840, 1100), (740, 1000)),
      ((740, 1000), (640, 900)),
      ((840, 1100), (640, 900)),
    ] {
      assert_eq!(
        FixedWindowSize::from_probes(
          (740, 1000),
          larger,
          smaller,
          true,
          true,
        ),
        FixedWindowSize::default(),
      );
    }
  }

  #[test]
  fn only_classifies_dimensions_that_were_probed() {
    assert_eq!(
      FixedWindowSize::from_probes(
        (740, 1000),
        (740, 1000),
        (740, 1000),
        false,
        true,
      ),
      FixedWindowSize {
        width: None,
        height: Some(1000)
      },
    );
  }
}
