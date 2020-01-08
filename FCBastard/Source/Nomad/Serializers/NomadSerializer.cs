using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Nomad
{
    public enum ContextStateType
    {
        Begin,

        Object,
        Member,

        End,
    }

    public delegate void ContextWriterDelegate(string message);

    public class NomadContext
    {
        static void DefaultPrinter(string message)  => Console.WriteLine(message);
        static void TracePrinter(string message)    => Trace.WriteLine(message);
        static void DebugPrinter(string message)    => Debug.WriteLine(message);
        
        public static ContextWriterDelegate PrintDelegate = DefaultPrinter;
        public static ContextWriterDelegate TraceDelegate = TracePrinter;
        public static ContextWriterDelegate DebugDelegate = DebugPrinter;

        List<NomadData> m_Refs;
        List<long> m_Ptrs;
        
        public ContextStateType State = ContextStateType.Begin;

        public int ObjectIndex = -1;
        public int MemberIndex = -1;

        public void Reset()
        {
            State = ContextStateType.Begin;
            
            ObjectIndex = -1;
            MemberIndex = -1;

            m_Refs = new List<NomadData>();
            m_Ptrs = new List<long>();
        }

        public void Begin()
        {
            if (State == ContextStateType.End)
                Reset();
        }

        public void End()
        {
            State = ContextStateType.End;
        }

        public void Log(string message)
        {
            if (PrintDelegate != null)
                PrintDelegate(message);
        }

        public void LogTrace(string message)
        {
            if (TraceDelegate != null)
                TraceDelegate(message);
        }

        public void LogDebug(string message)
        {
            if (DebugDelegate != null)
                DebugDelegate(message);
        }

        // does NOT do any checking!
        public int AddRef(NomadData data, long ptr)
        {
            var index = m_Refs.Count;

            m_Refs.Add(data);
            m_Ptrs.Add(ptr);

            return index;
        }

        public long GetPtr(NomadData data)
        {
            var idx = GetIdx(data);

            if (idx != -1)
                return m_Ptrs[idx];

            return -1;
        }

        public long GetPtrByIdx(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException("Reference index cannot be less-than zero.");

            if (index < m_Ptrs.Count)
                return m_Ptrs[index];

            return -1;
        }

        public int GetIdx(NomadData data)
        {
            return m_Refs.IndexOf(data);
        }

        public NomadData GetRefByPtr(long ptr)
        {
            var idx = m_Ptrs.IndexOf(ptr);
            
            if (idx != -1)
                return m_Refs[idx];

            return null;
        }

        public NomadData GetRefByIdx(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException("Reference index cannot be less-than zero.");

            if (index < m_Refs.Count)
                return m_Refs[index];

            return null;
        }
        
        public NomadContext()
        {
            Reset();
        }
    }

    public interface INomadSerializer
    {
        FileType Type { get; }
        FormatType Format { get; set; }
        
        void Serialize(Stream stream, NomadObject data);
        NomadObject Deserialize(Stream stream);
    }

    public interface INomadXmlFileSerializer : INomadSerializer
    {
        void LoadXml(string filename);
        void SaveXml(string filename);
    }
    
    public abstract class NomadSerializer : INomadSerializer
    {
        protected NomadContext Context { get; }
        
        public abstract FileType Type { get; }
        
        public FormatType Format { get; set; }
        
        public abstract void Serialize(Stream stream, NomadObject data);
        public abstract NomadObject Deserialize(Stream stream);
        
        protected NomadSerializer()
        {
            Context = new NomadContext();
        }
    }
}