package com.v2ray.ang.ui

import com.v2ray.ang.dto.entities.SubscriptionCache
import com.v2ray.ang.dto.entities.SubscriptionItem
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.random.Random

class SumrakHomeListBuilderSortTest {

  @Test
  fun subscriptionComparator_sortsLargeMixedPinSet_withoutTimSortContractFailure() {
    val pinOrder = mapOf(
      "pin-b" to 0,
      "pin-a" to 1,
      "stale-pin" to 2,
    )
    val comparator = SumrakHomeListBuilder.subscriptionSortComparator(pinOrder)
  val subs = (1..80).flatMap { i ->
        listOf(
          sub("guid-$i", remarks = "Group ${i % 5}"),
          sub("guid-$i-dup", remarks = "Group ${i % 5}"),
          sub("pin-a", remarks = "Pinned A"),
          sub("pin-b", remarks = "Pinned B"),
        )
      }.distinctBy { it.guid }

    repeat(25) { seed ->
      val sorted = subs.shuffled(Random(seed)).sortedWith(comparator)
      assertEquals(subs.size, sorted.size)
      assertTrue(sorted.map { it.guid }.distinct().size == sorted.size)
    }
  }

  @Test
  fun subscriptionComparator_pinnedItemsRespectPinOrder() {
    val pinOrder = mapOf("third" to 2, "first" to 0, "second" to 1)
    val comparator = SumrakHomeListBuilder.subscriptionSortComparator(pinOrder)
    val sorted = listOf(
      sub("z", remarks = "z"),
      sub("second", remarks = "s"),
      sub("first", remarks = "f"),
      sub("third", remarks = "t"),
    ).sortedWith(comparator)

    assertEquals(listOf("first", "second", "third", "z"), sorted.map { it.guid })
  }

  private fun sub(guid: String, remarks: String): SubscriptionCache {
    return SubscriptionCache(guid, SubscriptionItem(remarks = remarks))
  }
}