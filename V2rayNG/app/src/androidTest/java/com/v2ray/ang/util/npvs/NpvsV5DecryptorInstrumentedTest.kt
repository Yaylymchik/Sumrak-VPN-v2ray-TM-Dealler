package com.v2ray.ang.util.npvs

import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class NpvsV5DecryptorInstrumentedTest {

    @Test
    fun decryptsVpnTelekomFixture() {
        val context = ApplicationProvider.getApplicationContext<android.content.Context>()
        val raw = javaClass.classLoader!!.getResourceAsStream("vpn_telekom.npvs")!!.readBytes()
        val result = NpvsV5Decryptor.decrypt(context, raw)
        assertTrue(result.v2rayJsonConfigs.isNotEmpty() || result.shareLinks.isNotEmpty())
    }
}