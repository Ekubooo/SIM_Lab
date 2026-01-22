### QA for Radix.cs

- todo: dispatch number incorrect. (upper layer)(check)
- todo: is PNum needed? (check compute shader)
- todo: can GroupSize change?
- todo: hash calculate incorrect. (now model by PNum)
- todo: change para name at last. (when everything done)
- do cell key and PIndex need init in pass loop?
  - no because data in pass loop is not the result; and will resulted after 8-pass.
  - every sim_step reCalculating the cellkey-hash, and **PIndex**.

- how to padding?
  - padding at hash kernel: input size = sorting buffer.

- where to init index? already has cellkeys.
  - in hash kernel now.

- ComputeHelper.Dispatch(cs, count, kernelIndex: cs.FindKernel("CopyBack"));
  - need ! if not using setBuffer to switch buffer.
  - options : setBuffer switch or kernel CopyBack switch.****