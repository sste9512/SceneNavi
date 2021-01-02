/*
 * Heavily based on mips-eval.c by spinout (2010-06-13) as used in ZSaten
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SceneNavi.RomHandlers;

namespace SceneNavi.ActorRendering
{
    public class MipsEvaluator
    {
        public class OvlSections
        {
            public byte[] Text { get; private set; }
            public byte[] Data { get; private set; }
            public byte[] Rodata { get; private set; }
            public byte[] Bss { get; private set; }
            public uint TextVa { get; private set; }
            public uint DataVa { get; private set; }
            public uint RodataVa { get; private set; }
            public uint BssVa { get; private set; }

            public OvlSections(BaseRomHandler baseRom, ROMHandler.DmaTableEntry dma, uint vstart)
            {
                var ovl = new byte[dma.PEnd - dma.PStart];
                Buffer.BlockCopy(baseRom.Rom.Data, (int)dma.PStart, ovl, 0, ovl.Length);

                int indent, secaddr;
                indent = (int)Endian.SwapUInt32(BitConverter.ToUInt32(ovl, ovl.Length - 4));
                secaddr = (ovl.Length - indent);

                Text = new byte[Endian.SwapUInt32(BitConverter.ToUInt32(ovl, secaddr))];
                TextVa = vstart;
                Buffer.BlockCopy(ovl, (int)TextVa, Text, 0, Text.Length);

                Data = new byte[Endian.SwapUInt32(BitConverter.ToUInt32(ovl, secaddr + 4))];
                DataVa = (uint)(TextVa + Text.Length);
                Buffer.BlockCopy(ovl, (int)DataVa, Data, 0, Data.Length);

                Rodata = new byte[Endian.SwapUInt32(BitConverter.ToUInt32(ovl, secaddr + 8))];
                RodataVa = (uint)(DataVa + Data.Length);
                Buffer.BlockCopy(ovl, (int)RodataVa, Rodata, 0, Rodata.Length);

                Bss = new byte[Endian.SwapUInt32(BitConverter.ToUInt32(ovl, secaddr + 12))];
                BssVa = (uint)(RodataVa + Rodata.Length);
                Buffer.BlockCopy(ovl, (int)BssVa, Bss, 0, Bss.Length);
            }
        }

        public class MemoryRegion
        {
            public byte[] Data { get; private set; }
            public uint Address { get; private set; }

            public MemoryRegion(byte[] data, uint adr)
            {
                Address = adr;
                Data = data;
            }
        }

        public class SpecialOp
        {
            public uint Opcode { get; private set; }
            public uint Mask { get; private set; }
            public uint Value { get; private set; }

            public SpecialOp(uint op, uint mask, uint val)
            {
                Opcode = op;
                Mask = mask;
                Value = val;
            }
        }

        public enum ResultLocation { Internal, External, CodeFile };

        public class Result
        {
            public uint OpcodeAddress { get; set; }
            public uint TargetAddress { get; set; }
            public uint[] Arguments { get; set; }

            public Result(uint opadr, uint tgtadr, uint[] args)
            {
                OpcodeAddress = opadr;
                TargetAddress = tgtadr;
                Arguments = args;
            }

            public override string ToString()
            {
                return string.Format("(Loc: {0:X8} Tgt: {1:X8} Args: {2:X8} {3:X8} {4:X8} {5:X8})", OpcodeAddress, TargetAddress, Arguments[0], Arguments[1], Arguments[2], Arguments[3]);
            }
        }

        uint _baseAddress;
        uint[] _registers;
        uint[] _stack;
        int _stackPos;
        OvlSections _sections;
        List<MemoryRegion> _memoryMap;
        List<SpecialOp> _specialOps;

        public List<uint> Watches { get; private set; }
        public List<Result> Results { get; private set; }

        public delegate void RegisterHookDelegate(uint[] regs);
        private RegisterHookDelegate _registerHook;

        public MipsEvaluator(BaseRomHandler baseRom, ROMHandler.DmaTableEntry dma, uint ramadr, RegisterHookDelegate reghook = null, ushort var = 0)
        {
            _baseAddress = ramadr;

            _registers = new uint[32];
            _stack = new uint[256 * MIPS.SafetyVal];
            _stackPos = (int)(128 * MIPS.SafetyVal);

            _sections = new OvlSections(baseRom, dma, 0);

            _memoryMap = new List<MemoryRegion>
            {
                new MemoryRegion(_sections.Text, ramadr + _sections.TextVa),
                new MemoryRegion(_sections.Data, ramadr + _sections.DataVa),
                new MemoryRegion(_sections.Rodata, ramadr + _sections.RodataVa),
                new MemoryRegion(_sections.Bss, ramadr + _sections.BssVa)
            };

            _registerHook = reghook;

            _specialOps = new List<SpecialOp>();
            _specialOps.Add(new SpecialOp(MIPS.LH((uint)MIPS.Register.R0, 0x1C, (uint)MIPS.Register.A0), MIPS.LH((uint)MIPS.Register.R0, 0x1C, (uint)MIPS.Register.A0), var));
            _specialOps.Add(new SpecialOp(MIPS.LH((uint)MIPS.Register.R0, 0x1C, (uint)MIPS.Register.S0), MIPS.LH((uint)MIPS.Register.R0, 0x1C, (uint)MIPS.Register.A0), var));

            Watches = new List<uint>();
        }

        public void BeginEvaluation()
        {
            Results = new List<Result>();

            for (var i = 0; i < _sections.Text.Length; i += 4)
            {
                Evaluate(_sections.Text, i);
                _registers[0] = 0;
            }
        }

        private void Evaluate(byte[] words, int pos)
        {
            var word = Endian.SwapUInt32(BitConverter.ToUInt32(words, pos));
            uint imm = 0, calcadr = 0, target = 0;

            foreach (var sop in _specialOps)
            {
                if ((word & sop.Mask) == sop.Opcode)
                {
                    if ((word & 0xFC000000) == 0)
                        _registers[MIPS.GetRD(word)] = sop.Value;
                    else
                        _registers[MIPS.GetRT(word)] = sop.Value;
                    return;
                }
            }

            switch ((MIPS.Opcode)((word >> 26) & 0x3F))
            {
                case MIPS.Opcode.JAL:
                    target = MIPS.GetTARGET(word);
                    Evaluate(words, pos + 4);
                    ReportResult(target, pos);
                    Array.Clear(_registers, 1, 15);
                    Array.Clear(_registers, 24, 4);

                    _registers[(int)MIPS.Register.RA] = (_baseAddress & 0xFFFFFF) + (uint)(pos + 4);

                    foreach (var mem in _memoryMap)
                    {
                        if (target > (mem.Address & 0xFFFFFF) && mem.Data.Length + (mem.Address & 0xFFFFFF) > target)
                        {
                            pos = (int)(target - (_baseAddress & 0xFFFFFF));
                            break;
                        }
                    }
                    break;

                case MIPS.Opcode.SLLV:
                    _registers[MIPS.GetRD(word)] = _registers[MIPS.GetRT(word)] << (int)MIPS.GetRS(word);
                    break;

                case MIPS.Opcode.ADDIU:
                    imm = MIPS.GetIMM(word);
                    if ((imm & 0x8000) != 0) imm |= 0xFFFF0000;
                    _registers[MIPS.GetRT(word)] = _registers[MIPS.GetRS(word)] + imm;
                    if (MIPS.GetRT(word) == MIPS.GetRS(word) && MIPS.GetRT(word) == (uint)MIPS.Register.SP) _stackPos += (short)imm;
                    break;

                case MIPS.Opcode.LUI:
                    _registers[MIPS.GetRT(word)] = MIPS.GetIMM(word) << 16;
                    break;

                case MIPS.Opcode.ANDI:
                    _registers[MIPS.GetRT(word)] = _registers[MIPS.GetRS(word)] & MIPS.GetIMM(word);
                    break;

                case MIPS.Opcode.ORI:
                    _registers[MIPS.GetRT(word)] = _registers[MIPS.GetRS(word)] | MIPS.GetIMM(word);
                    break;

                case MIPS.Opcode.SW:
                    if (MIPS.GetRS(word) == (uint)MIPS.Register.SP) _stack[_stackPos + MIPS.GetIMM(word)] = _registers[MIPS.GetRT(word)];
                    break;
                /*
            case MIPS.Opcode.LH:
                imm = MIPS.GetIMM(word);
                calcadr = imm + Registers[MIPS.GetRS(word)];

                if (MIPS.GetRS(word) == (uint)MIPS.Register.SP)
                {
                    Registers[MIPS.GetRT(word)] = Stack[StackPos + imm];
                    break;
                }

                foreach (MemoryRegion mem in MemoryMap)
                {
                    if (calcadr > mem.Address && mem.Data.Length + mem.Address > calcadr)
                    {
                        Registers[MIPS.GetRT(word)] = (uint)Endian.SwapInt16(BitConverter.ToInt16(mem.Data, (int)(calcadr - mem.Address)));
                        break;
                    }
                }
                break;
                */
                case MIPS.Opcode.LW:
                    imm = MIPS.GetIMM(word);
                    if ((imm & 0x8000) != 0) imm |= 0xFFFF0000;
                    calcadr = imm + _registers[MIPS.GetRS(word)];

                    if (MIPS.GetRS(word) == (uint)MIPS.Register.SP)
                    {
                        _registers[MIPS.GetRT(word)] = _stack[_stackPos + imm];
                        break;
                    }

                    foreach (var mem in _memoryMap)
                    {
                        if (calcadr > mem.Address && mem.Data.Length + mem.Address > calcadr)
                        {
                            _registers[MIPS.GetRT(word)] = Endian.SwapUInt32(BitConverter.ToUInt32(mem.Data, (int)(calcadr - mem.Address)));
                            break;
                        }
                    }
                    break;
                /*
            case MIPS.Opcode.LHU:
                imm = MIPS.GetIMM(word);
                if ((imm & 0x8000) != 0) imm |= 0xFFFF0000; //????
                calcadr = imm + Registers[MIPS.GetRS(word)];

                if (MIPS.GetRS(word) == (uint)MIPS.Register.SP)
                {
                    Registers[MIPS.GetRT(word)] = Stack[StackPos + imm];
                    break;
                }

                foreach (MemoryRegion mem in MemoryMap)
                {
                    if (calcadr > mem.Address && mem.Data.Length + mem.Address > calcadr)
                    {
                        Registers[MIPS.GetRT(word)] = Endian.SwapUInt16(BitConverter.ToUInt16(mem.Data, (int)(calcadr - mem.Address)));
                        break;
                    }
                }
                break;
                */
                case MIPS.Opcode.TYPE_R:
                    {
                        switch ((MIPS.Opcode_R)(word & 0x3F))
                        {
                            case MIPS.Opcode_R.SLL:
                                _registers[MIPS.GetRD(word)] = _registers[MIPS.GetRT(word)] << (int)MIPS.GetSA(word);
                                break;
                            case MIPS.Opcode_R.SRA:
                            case MIPS.Opcode_R.SRL: /*test!*/
                                _registers[MIPS.GetRD(word)] = _registers[MIPS.GetRT(word)] >> (int)MIPS.GetSA(word);
                                break;
                            case MIPS.Opcode_R.ADDU:
                                _registers[MIPS.GetRD(word)] = _registers[MIPS.GetRT(word)] + _registers[MIPS.GetRS(word)];
                                break;
                            case MIPS.Opcode_R.SUBU:
                                _registers[MIPS.GetRD(word)] = _registers[MIPS.GetRT(word)] - _registers[MIPS.GetRS(word)];
                                break;
                            case MIPS.Opcode_R.AND:
                                _registers[MIPS.GetRD(word)] = _registers[MIPS.GetRT(word)] & _registers[MIPS.GetRS(word)];
                                break;
                            case MIPS.Opcode_R.OR:
                                _registers[MIPS.GetRD(word)] = _registers[MIPS.GetRT(word)] | _registers[MIPS.GetRS(word)];
                                break;
                            case MIPS.Opcode_R.XOR:
                                _registers[MIPS.GetRD(word)] = _registers[MIPS.GetRT(word)] ^ _registers[MIPS.GetRS(word)];
                                break;
                            case MIPS.Opcode_R.JR:
                                if (MIPS.GetRS(word) == (uint)MIPS.Register.RA)
                                {
                                    Array.Clear(_registers, 1, 15);
                                    Array.Clear(_registers, 24, 4);
                                    pos = (int)_registers[(int)MIPS.Register.RA];
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    break;

                default:
                    break;
            }

            _registerHook?.Invoke(_registers);
        }

        private void ReportResult(uint target, int pos)
        {
            if (Watches.Count != 0 && Watches.Find(x => (x & 0x0FFFFFFF) == (target & 0x0FFFFFFF)) == 0) return;

            var args = new uint[4];
            for (var i = 0; i < args.Length; i++) args[i] = _registers[(int)MIPS.Register.A0 + i];

            Results.Add(new Result((uint)pos, (target & 0x0FFFFFFF), args));
        }
    }
}
