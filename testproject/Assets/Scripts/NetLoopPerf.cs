using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class NetLoopPerf : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartCoroutine(Benchmark(128));
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            StartCoroutine(Benchmark(1024));
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            StartCoroutine(Benchmark(8 * 1024));
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            StartCoroutine(Benchmark(16 * 1024));
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            StartCoroutine(Benchmark(32 * 1024));
        }
    }

    private IEnumerator Benchmark(int count)
    {
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)}(count: {count})");

        var sw = new Stopwatch();

        var gobjs = new List<GameObject>();
        var comps = new List<NetLoopComp>();
        for (int i = 0; i < count; i++)
        {
            var gobj = new GameObject($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} #{i}");
            var comp = gobj.AddComponent<NetLoopComp>();
            gobjs.Add(gobj);
            comps.Add(comp);
        }

        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < count; i++)
        {
            comps[i].RegisterUpdates(i);
        }

        sw.Stop();
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> register: {sw.ElapsedMilliseconds}ms");

        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForEndOfFrame();
        }

        sw.Stop();
        long t1 = sw.ElapsedMilliseconds;
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> 10-frames: {t1}ms");

        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForEndOfFrame();
        }

        sw.Stop();
        long t2 = sw.ElapsedMilliseconds;
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> 10-frames: {t2}ms");

        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForEndOfFrame();
        }

        sw.Stop();
        long t3 = sw.ElapsedMilliseconds;
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> 10-frames: {t3}ms");

        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForEndOfFrame();
        }

        sw.Stop();
        long t4 = sw.ElapsedMilliseconds;
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> 10-frames: {t4}ms");

        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < 10; i++)
        {
            yield return new WaitForEndOfFrame();
        }

        sw.Stop();
        long t5 = sw.ElapsedMilliseconds;
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> 10-frames: {t5}ms");

        yield return new WaitForEndOfFrame();

        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> avg-frames: {(t1 + t2 + t3 + t4 + t5) / 5}ms");
        
        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < count; i++)
        {
            comps[i].UnregisterUpdates();
        }

        sw.Stop();
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> unregister: {sw.ElapsedMilliseconds}ms");

        yield return new WaitForEndOfFrame();

        for (int i = 0; i < count; i++)
        {
            DestroyImmediate(gobjs[i]);
        }

        gobjs.Clear();
        comps.Clear();
    }
}